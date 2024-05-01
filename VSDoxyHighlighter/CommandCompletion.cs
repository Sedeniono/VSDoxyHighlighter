using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.Editor;
using EnvDTE;
using Microsoft.VisualStudio.VCCodeModel;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System;


namespace VSDoxyHighlighter
{
  //================================================================================
  // CommentCommandCompletionSource
  //================================================================================

  // Based on https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/AsyncCompletion
  [Export(typeof(IAsyncCompletionSourceProvider))]
  [Name("VSDoxyHighlighterCommandCompletionSourceProvider")]
  [ContentType("C/C++")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  class CommentCommandCompletionSourceProvider : IAsyncCompletionSourceProvider
  {
    private Dictionary<ITextView, CommentCommandCompletionSource> mCache = new Dictionary<ITextView, CommentCommandCompletionSource>();
    
    [Import]
    private IVsEditorAdaptersFactoryService mAdapterService = null;

    /// <summary>
    /// More or less called by VS whenever the user created a new view of some document and starts typing 
    /// in there for the first time.
    /// </summary>
    public IAsyncCompletionSource GetOrCreate(ITextView textView)
    {
      if (mCache.TryGetValue(textView, out var itemSource)) {
        return itemSource;
      }

      var source = new CommentCommandCompletionSource(mAdapterService, textView);
      textView.Closed += (o, e) => mCache.Remove(textView);
      mCache.Add(textView, source);
      return source;
    }
  }


  struct VsObjectList 
  {
    public VsObjectList(IVsObjectList2 list) 
    {
      mList = list;
      Count = 0;
      if (mList?.GetItemCount(out uint count) == VSConstants.S_OK) {
        Count = count;
      }
    }

    public uint Count { get; private set; }

    public string GetName(uint idx)
    { 
      if (mList?.GetProperty(idx, (int)_VSOBJLISTELEMPROPID.VSOBJLISTELEMPROPID_FULLNAME, out object nameObj) == VSConstants.S_OK) {
        return nameObj as string;
      }
      return null;
    }

    public VsObjectList? GetChildren(uint idx, _LIB_LISTTYPE childrenType) 
    {
      if (mList?.GetList2(idx, (uint)childrenType, (uint)_LIB_LISTFLAGS.LLF_DONTUPDATELIST, null, out IVsObjectList2 children)
            == VSConstants.S_OK && children != null) {
        return new VsObjectList(children);
      }
      return null;
    }

    private readonly IVsObjectList2 mList;
  }


  class VsObjectManager 
  {
    public VsObjectManager(IVsObjectManager2 objectManager) 
    {
      objectManager?.FindLibrary(new Guid(BrowseLibraryGuids80.VC), out mLibrary);
      mSearchCriteria = new[] { new VSOBSEARCHCRITERIA2() };
    }

    public VsObjectList? Classes
    {
      get {
        if (mLibrary?.GetList2(
              (uint)_LIB_LISTTYPE.LLT_CLASSES,
              (uint)_LIB_LISTFLAGS.LLF_DONTUPDATELIST,
              mSearchCriteria, out var list) == VSConstants.S_OK) {
          return new VsObjectList(list);
        }
        return null;
      }
    }

    private readonly IVsLibrary2 mLibrary = null;
    private readonly VSOBSEARCHCRITERIA2[] mSearchCriteria;
  }


  /// <summary>
  /// Defines when the autocomplete box should appear as well as its content.
  /// Note: The instance is reused for every autocomplete operation in the same text view.
  /// Based on https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/AsyncCompletion
  /// </summary>
  class CommentCommandCompletionSource : IAsyncCompletionSource
  {
    private IVsObjectManager2 mObjectManager2 = null;


    public CommentCommandCompletionSource(IVsEditorAdaptersFactoryService adapterService, ITextView textView) 
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      mObjectManager2 = VSDoxyHighlighterPackage.GetGlobalService(typeof(SVsObjectManager)) as IVsObjectManager2;

      mTextView = textView;
      mAdapterService = adapterService;

      // We don't subscribe to change events of the options or the parser: Attempting to change the content
      // of a shown box/tooltip if the user changes some settings makes no sense since the user cannot really
      // change the settings in the options page while a box/tooltip is shown. But even if, the box/tooltip is
      // so short lived that doing anything special (like closing the box/tooltip) is simply not worth the effort.
      mGeneralOptions = VSDoxyHighlighterPackage.GeneralOptions;
      mCommentParser = VSDoxyHighlighterPackage.CommentParser;
    }

    /// <summary>
    /// Called by VS on the UI thread whenever a new autocomplete "box" might pop up.
    /// </summary>
    /// <param name="trigger">The character typed by the user.</param>
    /// <param name="triggerLocation"> Basically the index after the type character. For example, typing "\" at 
    ///     the very start of the document results in triggerLocation.Position==1.</param>
    /// <param name="token"></param>
    /// <returns>We must basically return a span that covers the string which contains the start of the doxygen command.
    /// For example, if a line is "foo \br", and the user types an "i" at the end, we need to return the span for "\bri".
    /// That causes VS to jump to the commands starting with "\bri" in the autocomplete box.</returns>
    public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
    {
      if (!mGeneralOptions.EnableAutocomplete) {
        return CompletionStartData.DoesNotParticipateInCompletion;
      }

      ITextSnapshot snapshot = triggerLocation.Snapshot;
      if (triggerLocation.Position > snapshot.Length) {
        return CompletionStartData.DoesNotParticipateInCompletion;
      }

      for (int pos = triggerLocation.Position - 1; pos >= 0; --pos) {
        char c = snapshot.GetText(pos, 1)[0];
        // Check whether we hit the start of a text fragment that might be a doxygen command (\ or @) or a parameter (space or tab).
        if (c == '\\' || c == '@' || c == ' ' || c == '\t') {
          // Also check whether it is actually inside of a comment. Since this is the more expensive check,
          // it is performed afterwards.
          if (IsLocationInEnabledCommentType(triggerLocation)) {
            // If the user typed e.g. "\br", we return the span for "br". If the user typed "\", we return a span of length 0.
            // By stripping the "\" or "@" from the returned span, we achieve two things:
            // 1) GetCompletionContextAsync() only needs to return each command once, instead of twice for the "\" and "@" variant.
            //    This is because VS will use e.g. the "br" string to match the completion items. This simplifies the code a bit.
            // 2) More importantly, Visual Studio closes the autocomplete box if the user backspaces till
            //    the "\" or "@" is removed. If we included the "\" or "@" in the applicableToSpan, VS does not close it for some reason.
            SnapshotSpan applicableToSpan = new SnapshotSpan(snapshot, pos + 1, triggerLocation.Position - pos - 1);
            return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableToSpan);
          }
        }
        // We stop looking at the beginning of the line.
        else if (c == '\n' || c == '\r') {
          return CompletionStartData.DoesNotParticipateInCompletion;
        }
      }

      return CompletionStartData.DoesNotParticipateInCompletion;
    }


    /// <summary>
    /// Called by VS once per completion session on a background thread to fetch the set of all completion 
    /// items available at the given location.
    /// </summary>
    public async Task<CompletionContext> GetCompletionContextAsync(
      IAsyncCompletionSession session, 
      CompletionTrigger trigger, 
      SnapshotPoint triggerLocation, 
      SnapshotSpan applicableToSpan, 
      CancellationToken cancellationToken)
    {
      if (!mGeneralOptions.EnableAutocomplete) {
        return CompletionContext.Empty;
      }

      ImmutableArray<CompletionItem>.Builder itemsBuilder = null;
      if (applicableToSpan.Start.Position > 0) {
        SnapshotPoint startPoint = applicableToSpan.Start.Subtract(1);
        char startChar = startPoint.GetChar();
        // '\' and '@' start a command.
        if (startChar == '\\' || startChar == '@') {
          itemsBuilder = PopulateAutcompleteBoxWithCommands(startChar);
        }
        // If the user typed a whitespace, we check whether it happend after a Doxygen command for which we
        // support autocompletion of the parameter, and if yes, populate the autocomplete box with possible parameter values.
        else if (startChar == ' ' || startChar == '\t') {
          itemsBuilder = await PopulateAutocompleteBoxForParameterAsync(startPoint, cancellationToken);
        }
      }

      if (itemsBuilder != null) {
        return new CompletionContext(itemsBuilder.ToImmutable(), null);
      }
      else {
        return CompletionContext.Empty;
      }
    }


    /// <summary>
    /// Called by VS on a background thread to get the tooltip description for the specific <paramref name="item"/>.
    /// </summary>
    public /*async*/ Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
    {
      if (item.Properties.TryGetProperty(typeof(DoxygenHelpPageCommand), out DoxygenHelpPageCommand cmd)) {
        // We don't show hyperlinks because Visual Studio does not allow clicking on them.
        var description = AllDoxygenHelpPageCommands.ConstructDescription(mCommentParser, cmd, showHyperlinks: false);
        return Task.FromResult<object>(description);
      }

      return Task.FromResult<object>("");
    }


    private bool IsLocationInEnabledCommentType(SnapshotPoint triggerLocation)
    {
      // Exploit the existing CommentClassifier associated with the text buffer to figure out whether
      // the location is in a comment type where autocomplete should be enabled. The CommentClassifier
      // got created by Visual Studio via CommentClassifierProvider.GetClassifier() when the text
      // buffer got created.
      if (triggerLocation.Snapshot.TextBuffer.Properties.TryGetProperty(
                typeof(CommentClassifier), out CommentClassifier commentClassifier)) {
        CommentType? commentType = commentClassifier.CommentExtractor.GetTypeOfCommentBeforeLocation(triggerLocation);
        if (commentType != null) {
          return mGeneralOptions.IsEnabledInCommentType(commentType.Value);
        }
      }
      else {
        Debug.Assert(false);
      }

      return false;
    }


    private ImmutableArray<CompletionItem>.Builder PopulateAutcompleteBoxWithCommands(char startChar)
    {
      var itemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();

      int numCommands = DoxygenCommandsGeneratedFromHelpPage.cCommands.Length;
      int curCommandNumber = 1;

      foreach (DoxygenHelpPageCommand cmd in AllDoxygenHelpPageCommands.cAmendedDoxygenCommands) {
        var item = new CompletionItem(
          // Add the "\" or "@" since it is not actually part of the autocompleted command, because
          // the applicableToSpan does not cover it. See InitializeCompletion() for the reason.
          displayText: startChar + cmd.Command,
          source: this,
          icon: cCompletionImage,
          filters: ImmutableArray<CompletionFilter>.Empty,
          suffix: cmd.Parameters,
          insertText: cmd.Command,
          // sortText: Sorting is done using Enumerable.OrderBy, which apparently calls String.CompareTo(),
          // which is doing a case-sensitive and culture-sensitive comparison using the current culture.
          // But we want to keep the sorting of our list of commands. Padding with 0 to the left to get
          // e.g. 11 to be sorted after 1.
          sortText: curCommandNumber.ToString().PadLeft(numCommands, '0'),
          filterText: cmd.Command,
          automationText: cmd.Command,
          attributeIcons: ImmutableArray<ImageElement>.Empty);

        // Add a reference to the DoxygenHelpPageCommand to the item so that we can access it in GetDescriptionAsync().
        item.Properties.AddProperty(typeof(DoxygenHelpPageCommand), cmd);

        itemsBuilder.Add(item);
        ++curCommandNumber;
      }

      return itemsBuilder;
    }


    private async Task<ImmutableArray<CompletionItem>.Builder> PopulateAutocompleteBoxForParameterAsync(
        SnapshotPoint startPoint, CancellationToken cancellationToken)
    {
      string command = TryGetDoxygenCommandBeforeWhitespace(startPoint);
      if (command == null) {
        return null;
      }

      // TODO: Add option page to disable the parameter completion. Reason: It requires quite a bunch of semantic infos,
      // and getting them might be expensive. So we want the user to disable the feature.
      // TODO: Add to readme: Doesn't work for NTTP template arguments in global function declerations.
      // TODO: Can we provide a list of all classes/structs, namespaces, functions, macros, etc. for the corresponding doxygen commands?
      // TODO: Make \param and \tparam more intelligent (suggest the next param first)
      // TODO: \exception idea: scan the definition for throws
      // TODO: Idea for "global" lists: Add filters (only in file; only in current namespace)
      // TODO: Check for IsZombie everywhere?
      // TODO: Catch COMExceptions
      // TODO: Performance. Especially because every VS access is necessarily on the main thread due to COM stuff.
      //       Querying the cancellationToken from the main thread is useless; it never gets set.
      //       But maybe we can somehow temporarily switch away from the main thread between COM queries, so that the message loop etc runs?
      //       But before doing this, check with LLVM code base, if necessary.
      //       Or: Implement in native C++ code; maybe it helps?

      IEnumerable<string> elementsToShow = null;
      string parentName = null;
      ImageElement icon = null;

      if (command == "p" || command == "a" 
          || command == "param" || command == "param[in]" || command == "param[out]" || command == "param[in,out]") {
        // Need to switch to main thread for the CodeModel. Well, actually after we have got the CodeModel on the
        // main thread, we could access its functions/properties from any thread. Behind the scenes, an automatic
        // switch to the main thread happens. However, this happens for every single access of the CodeModel, which
        // results in very bad performance. So switch once at the beginning.
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var cppFileSemantics = new CppFileSemanticsFromVSCodeModelAndCache(mAdapterService, mTextView.TextBuffer);
        FunctionInfo funcInfo = cppFileSemantics.TryGetFunctionInfoIfNextIsAFunction(startPoint);
        icon = cParamImage;
        if (funcInfo != null) {
          elementsToShow = funcInfo.ParameterNames;
          parentName = funcInfo.FunctionName;
        }
        else {
          MacroInfo macroInfo = cppFileSemantics.TryGetMacroInfoIfNextIsAMacro(startPoint);
          if (macroInfo != null) {
            elementsToShow = macroInfo.Parameters;
            parentName = macroInfo.MacroName;
          }
        }
      }
      else if (command == "tparam") {
        // As above: Switch to the main thread for the CodeModel.
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var cppFileSemantics = new CppFileSemanticsFromVSCodeModelAndCache(mAdapterService, mTextView.TextBuffer);
        FunctionInfo funcInfo = cppFileSemantics.TryGetFunctionInfoIfNextIsAFunction(startPoint);
        icon = cTemplateParamImage;
        if (funcInfo != null) {
          if (funcInfo.TemplateParameterNames.Count() > 0) {
            elementsToShow = funcInfo.TemplateParameterNames;
            parentName = funcInfo.FunctionName;
          }
        }
        else { 
          ClassInfo clsInfo = cppFileSemantics.TryGetClassInfoIfNextIsATemplateClassOrAlias(startPoint);
          if (clsInfo != null && clsInfo.TemplateParameterNames.Count() > 0) {
            elementsToShow = clsInfo.TemplateParameterNames;
            parentName = clsInfo.ClassName;
          }
        }
      }

      if (elementsToShow != null && elementsToShow.Count() > 0) {
        Debug.Assert(parentName != null);
        return CreateAutocompleteItemsForCommandParameter(elementsToShow, parentName, icon);
      }

      // TODO: The same thing for unions and structs, except that different properties need to be accessed.
      if (command == "class") {
        // As above: Switch to the main thread for the CodeModel.
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var newToOldMapper = new VisualStudioNewToOldTextBufferMapper(mAdapterService, mTextView.TextBuffer);
        // We only show classes in the current project. This is done for performance reasons, and because I guess that
        // it will be very rare that someone writes documentation for some class in another project.
        VCCodeModel cm = newToOldMapper.Document?.ProjectItem?.ContainingProject?.CodeModel as VCCodeModel;
        if (cm != null) {
          var itemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();

          Stopwatch sw = Stopwatch.StartNew();

          // TODO: What about classes in structs?
          // TODO: Filter duplicates?
          //    - Sometimes they are indeed duplicates.
          //    - Sometimes they are actually specializations. But VCCodeClass does not expose the info if something is a specialization.
          //      Reconstructing that info is hard. => Probably: Don't attempt to show specializations right now, although doxygen can
          //      handle them. Instead, just show the 'Name' once.

#if true
          // Try via CodeModel
          AddClassesToItemsRecursive(itemsBuilder, cm.Classes, "");
          AddClassesInNamespacesToItemsRecursive(itemsBuilder, cm.Namespaces, "");
#else
          // Try via IVsObjectManager2, to see if it is faster. Result: It is not faster.
          // https://github.com/EWSoftware/SHFB/blob/2201c42a9dea11b6855b7a6287f1c0f088da251b/SHFB/Source/VSIX_Shared/GoToDefinition/CodeEntitySearcher.cs#L46
          // Note: The C# definition of IVsObjectList2::GetText() is broken (https://stackoverflow.com/q/77404509/3740047).
          var objectManager = new VsObjectManager(mObjectManager2);
          AddClassesToItemsRecursive(itemsBuilder, objectManager.Classes, "");
          // TODO: Find in namespaces
#endif


          sw.Stop();

          itemsBuilder.Add(new CompletionItem(
            displayText: $"_Took {sw.Elapsed.TotalSeconds}s for {itemsBuilder.Count} items",
            source: this,
            icon: icon,
            filters: ImmutableArray<CompletionFilter>.Empty,
            suffix: "",
            insertText: "__",
            // As in PopulateAutcompleteBoxWithCommands(): Ensure we keep the order
            sortText: " ",
            filterText: "__",
            automationText: "__",
            attributeIcons: ImmutableArray<ImageElement>.Empty));

          // TODO: Tooltip: Top of the body? Precise location? Full declaration?
          // Line would be: cls.StartPoint.Line
          return itemsBuilder;
        }
      }

      return null;
    }


    private string TryGetDoxygenCommandBeforeWhitespace(SnapshotPoint whitespacePoint) 
    {
      ITextSnapshotLine line = whitespacePoint.GetContainingLine();
      string lineBeforeCompletionStart = line.GetText().Substring(0, whitespacePoint - line.Start).TrimEnd();
      int commandStartIdx = lineBeforeCompletionStart.LastIndexOfAny(new char[] { '@', '\\' });
      if (commandStartIdx >= 0) {
        return lineBeforeCompletionStart.Substring(commandStartIdx + 1);
      }
      return null;
    }


    private ImmutableArray<CompletionItem>.Builder CreateAutocompleteItemsForCommandParameter(
      IEnumerable<string> elementsToShow,
      string parentElementName,
      ImageElement icon) 
    {
      var itemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();
      int numParams = elementsToShow.Count();
      int curParamNumber = 1;
      foreach (string elem in elementsToShow) {
        var item = new CompletionItem(
          displayText: elem,
          source: this,
          icon: icon,
          filters: ImmutableArray<CompletionFilter>.Empty,
          suffix: parentElementName,
          insertText: elem,
          // As in PopulateAutcompleteBoxWithCommands(): Ensure we keep the order
          sortText: curParamNumber.ToString().PadLeft(numParams, '0'),
          filterText: elem,
          automationText: elem,
          attributeIcons: ImmutableArray<ImageElement>.Empty);

        itemsBuilder.Add(item);
        ++curParamNumber;
      }
      return itemsBuilder;
    }


    private void AddClassesToItemsRecursive(
      ImmutableArray<CompletionItem>.Builder itemsBuilder, 
      CodeElements classes, 
      string containingNamespace)
    {
      if (containingNamespace != "") {
        containingNamespace += "::";
      }
      foreach (CodeElement elem in classes) {
        if (elem is VCCodeClass cls) {
          string elemName = containingNamespace + cls.Name;
          var item = new CompletionItem(
            displayText: elemName,
            source: this,
            icon: cClassImage,
            filters: ImmutableArray<CompletionFilter>.Empty,
            // TODO: Do we really want to show the file (performance)??????
            suffix: default,
            //suffix: $"{cls.File} => {cls.StartPoint.Line}",
            insertText: elemName,
            sortText: elemName,
            filterText: elemName,
            automationText: elemName,
            attributeIcons: ImmutableArray<ImageElement>.Empty);

          itemsBuilder.Add(item);

          AddClassesToItemsRecursive(itemsBuilder, cls.Classes, elemName);
        }
      }
    }


    private void AddClassesInNamespacesToItemsRecursive(
      ImmutableArray<CompletionItem>.Builder itemsBuilder,
      CodeElements namespaces,
      string containingNamespace)
    {
      if (containingNamespace != "") {
        containingNamespace += "::";
      }
      foreach (CodeElement elem in namespaces) {
        if (elem is VCCodeNamespace ns) {
          string nsName = ns.Name;
          string nestedNsName = nsName != "`anonymous-namespace'" ? containingNamespace + nsName : containingNamespace;
          AddClassesToItemsRecursive(itemsBuilder, ns.Classes, nestedNsName);
          AddClassesInNamespacesToItemsRecursive(itemsBuilder, ns.Namespaces, nestedNsName);
        }
      }
    }


    private void AddClassesToItemsRecursive(
      ImmutableArray<CompletionItem>.Builder itemsBuilder,
      VsObjectList? classes,
      string containingNamespace)
    {
      if (!classes.HasValue) {
        return;
      }
      if (containingNamespace != "") {
        containingNamespace += "::";
      }
      var unpackedClasses = classes.Value;
      for (uint idx = 0; idx < unpackedClasses.Count; ++idx) {
        string elemName = unpackedClasses.GetName(idx);
        if (elemName != null) {
          elemName = containingNamespace + elemName;
          var item = new CompletionItem(
              displayText: elemName,
              source: this,
              icon: cClassImage,
              filters: ImmutableArray<CompletionFilter>.Empty,
              // TODO: Do we really want to show the file (performance)??????
              suffix: default,
              //suffix: $"{cls.File} => {cls.StartPoint.Line}",
              insertText: elemName,
              sortText: elemName,
              filterText: elemName,
              automationText: elemName,
              attributeIcons: ImmutableArray<ImageElement>.Empty);

          itemsBuilder.Add(item);

          AddClassesToItemsRecursive(itemsBuilder, unpackedClasses.GetChildren(idx, _LIB_LISTTYPE.LLT_CLASSES), elemName);
        }
      }
    }


    // http://glyphlist.azurewebsites.net/knownmonikers/
    // https://github.com/madskristensen/KnownMonikersExplorer
    private static ImageElement cCompletionImage = new ImageElement(KnownMonikers.CommentCode.ToImageId(), "Doxygen command");
    // Image for parameter and template parameter: These should be the images shown by VS's own IntelliSense in C++.
    private static ImageElement cParamImage = new ImageElement(KnownMonikers.FieldPublic.ToImageId(), "Doxygen parameter");
    private static ImageElement cTemplateParamImage = new ImageElement(KnownMonikers.TypeDefinition.ToImageId(), "Doxygen template parameter");
    private static ImageElement cClassImage = new ImageElement(KnownMonikers.Class.ToImageId(), "Doxygen class");

    private readonly ITextView mTextView;
    private readonly IVsEditorAdaptersFactoryService mAdapterService;
    private readonly IGeneralOptions mGeneralOptions;
    private readonly CommentParser mCommentParser;
  }


  //================================================================================
  // CommentCommandCompletionCommitManager
  //================================================================================

  [Export(typeof(IAsyncCompletionCommitManagerProvider))]
  [Name("VSDoxyHighlighterCommandCompletionCommitManagerProvider")]
  [ContentType("C/C++")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  class CommentCommandCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider
  {
    Dictionary<ITextView, CommentCommandCompletionCommitManager> mCache = new Dictionary<ITextView, CommentCommandCompletionCommitManager>();

    public IAsyncCompletionCommitManager GetOrCreate(ITextView textView)
    {
      if (mCache.TryGetValue(textView, out var itemSource)) {
        return itemSource;
      }

      var manager = new CommentCommandCompletionCommitManager();
      textView.Closed += (o, e) => mCache.Remove(textView);
      mCache.Add(textView, manager);
      return manager;
    }
  }

  
  /// <summary>
  /// Defines when an autocomplete session ends.
  /// </summary>
  class CommentCommandCompletionCommitManager : IAsyncCompletionCommitManager 
  {
    // When the user hits tab, enter or double-clicks, it inserts the current selection in the autocomplete box.
    // Additionally, we also do it when the user types a space. This especially ensures that in case the user
    // typed the command completely, the autocomplete box vanishes instead of staying up.
    private static ImmutableArray<char> cCommitChars = new char[] { ' ' }.ToImmutableArray();
    public IEnumerable<char> PotentialCommitCharacters => cCommitChars;

    public bool ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
    { 
      return true;
    }

    public CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
    {
      return CommitResult.Unhandled; // Rely on the default VS behavior
    }
  }
}

