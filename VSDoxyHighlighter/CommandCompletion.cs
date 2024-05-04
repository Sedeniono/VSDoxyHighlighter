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


  /// <summary>
  /// Defines when the autocomplete box should appear as well as its content.
  /// Note: The instance is reused for every autocomplete operation in the same text view.
  /// Based on https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/AsyncCompletion
  /// </summary>
  class CommentCommandCompletionSource : IAsyncCompletionSource
  {
    public CommentCommandCompletionSource(IVsEditorAdaptersFactoryService adapterService, ITextView textView) 
    {
      ThreadHelper.ThrowIfNotOnUIThread();

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
        // support autocompletion of the command's parameter, and if yes, populate the autocomplete box with possible parameter values.
        else if (AnyAdvancedAutocompleteEnabled() && (startChar == ' ' || startChar == '\t')) {
          try {
            itemsBuilder = await PopulateAutocompleteBoxForParameterOfDoxygenCommandAsync(startPoint, cancellationToken);
          }
          catch (Exception ex) {
            ActivityLog.LogError("VSDoxyHighlighter", $"Exception occurred while checking for parameter completion: {ex}");
          }
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


    private async Task<ImmutableArray<CompletionItem>.Builder> PopulateAutocompleteBoxForParameterOfDoxygenCommandAsync(
        SnapshotPoint startPoint, CancellationToken cancellationToken)
    {
      AutocompleteInfoForParameterOfDoxygenCommand? infos = await TryGetAutocompleteInfosForParameterOfDoxygenCommandAsync(startPoint, cancellationToken);
      if (infos != null && infos.Value.elementsToShow.Count() > 0) {
        return CreateAutocompleteItemsForParameterOfDoxygenCommand(infos.Value);
      }
      return null;
    }


    private struct AutocompleteInfoForParameterOfDoxygenCommand
    {
      public IEnumerable<(string name, string type)> elementsToShow;
      
      // E.g. the function or class name.
      public string parentInfo;
      
      // If an element in 'elementsToShow' matches this string, the next one after that will be preselected.
      // This is used to make writing parameter documentation more convenient. For example, if one is successively documenting
      // the function parameters with '@param', the autocomplete box will preselect the most likely next parameter one wants
      // to insert, i.e. the one which comes next after the previously documented parameter.
      public string elementBeforeElementToPreselect;
      
      public ImageElement icon;
    }


    private async Task<AutocompleteInfoForParameterOfDoxygenCommand?> TryGetAutocompleteInfosForParameterOfDoxygenCommandAsync(
      SnapshotPoint startPoint, CancellationToken cancellationToken)
    {
      // Get the Doxygen command without the "\" or "@".
      string command = TryGetDoxygenCommandBeforeWhitespace(startPoint);
      if (command == null) {
        return null;
      }

      if (mGeneralOptions.EnableFunctionAndMacroParameterAutocomplete) {
        // Autocompletion for Doxygen command parameters that document C++ parameters.
        if (cCmdsToDocumentParam.Contains(command)) {
          return await TryGetAutocompleteInfosForParamCommandAsync(startPoint, cCmdsToDocumentParam, preselectAfterPrevious: true, cancellationToken);
        }

        // Autocompletion for Doxygen command parameters that refer to C++ parameters in the running text.
        if (cCmdsToReferToParam.Contains(command)) {
          // Note: preselectAfterPrevious = false. We cannot sensibly guess which function parameter the user wants to preselect for "@p" and "@a".
          return await TryGetAutocompleteInfosForParamCommandAsync(startPoint, cCmdsToReferToParam, preselectAfterPrevious: false, cancellationToken);
        }
      }

      if (mGeneralOptions.EnableTemplateParameterAutocomplete) {
        // Autocompletion for Doxygen command parameters that document C++ template parameters.
        if (cCmdsToDocumentTParam.Contains(command)) {
          return await TryGetAutocompleteInfosForTParamCommandAsync(startPoint, preselectAfterPrevious: true, cancellationToken);
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


    // Gets parameter information for the Doxygen commands '@param', '@p' and '@a'.
    private async Task<AutocompleteInfoForParameterOfDoxygenCommand?> TryGetAutocompleteInfosForParamCommandAsync(
      SnapshotPoint startPoint, string[] paramCommands, bool preselectAfterPrevious, CancellationToken cancellationToken) 
    {
      // Need to switch to main thread for the CodeModel. Well, actually after we have gotten the CodeModel on the
      // main thread, we could access its functions/properties from any thread. Behind the scenes, an automatic
      // switch to the main thread happens (i.e. a message into the message loop of the main thread gets posted,
      // which then is executed, and is returned to us). This happens because CodeModel is implemented in native
      // code, and in this case the runtime does this automatic marshalling. However, this would happen for every
      // single access of the CodeModel, which results in very bad performance. So switch once at the beginning.
      // Compare https://devblogs.microsoft.com/premier-developer/asynchronous-and-multithreaded-programming-within-vs-using-the-joinabletaskfactory/#a-small-history-lesson-in-com-thread-marshaling
      // and https://github.com/dotnet/project-system/issues/924.
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

      var cppFileSemantics = new CppFileSemanticsFromVSCodeModelAndCache(mAdapterService, mTextView.TextBuffer);

      FunctionInfo funcInfo = cppFileSemantics.TryGetFunctionInfoIfNextIsAFunction(startPoint);
      if (funcInfo != null) {
        var result = new AutocompleteInfoForParameterOfDoxygenCommand {
          elementsToShow = funcInfo.Parameters.Select(p => (p.Name, p.Type)),
          parentInfo = $"Function: {funcInfo.FunctionName}",
          icon = cParamImage
        };
        if (preselectAfterPrevious) {
          FormattedFragmentGroup group = TryFindDoxygenCommandOnLinesBeforePoint(cppFileSemantics, startPoint, paramCommands);
          if (group != null && group.Fragments.Count == 2) {
            result.elementBeforeElementToPreselect = group.Fragments[1].GetText(startPoint.Snapshot);
          }
        }
        return result;
      }
      
      MacroInfo macroInfo = cppFileSemantics.TryGetMacroInfoIfNextIsAMacro(startPoint);
      if (macroInfo != null) {
        var result = new AutocompleteInfoForParameterOfDoxygenCommand { 
          elementsToShow = macroInfo.Parameters.Select(p => (p, "")),
          parentInfo = $"Macro: {macroInfo.MacroName}",
          icon = cParamImage
        };
        if (preselectAfterPrevious) {
          FormattedFragmentGroup group = TryFindDoxygenCommandOnLinesBeforePoint(cppFileSemantics, startPoint, paramCommands);
          if (group != null && group.Fragments.Count == 2) {
            result.elementBeforeElementToPreselect = group.Fragments[1].GetText(startPoint.Snapshot);
          }
        }
        return result;
      }

      return null;
    }


    // Gets parameter information for the Doxygen command '@tparam'.
    private async Task<AutocompleteInfoForParameterOfDoxygenCommand?> TryGetAutocompleteInfosForTParamCommandAsync(
      SnapshotPoint startPoint, bool preselectAfterPrevious, CancellationToken cancellationToken)
    {
      // As in TryGetAutocompleteInfosForParamCommandAsync(): Switch to the main thread for the FileCodeModel, especially because of performance.
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

      var cppFileSemantics = new CppFileSemanticsFromVSCodeModelAndCache(mAdapterService, mTextView.TextBuffer);

      FunctionInfo funcInfo = cppFileSemantics.TryGetFunctionInfoIfNextIsAFunction(startPoint);
      if (funcInfo != null) {
        if (funcInfo.TemplateParameters.Count() > 0) {
          var result = new AutocompleteInfoForParameterOfDoxygenCommand {
            elementsToShow = funcInfo.TemplateParameters.Select(p => (p, "")),
            parentInfo = $"Function: {funcInfo.FunctionName}",
            icon = cTemplateParamImage
          };

          if (preselectAfterPrevious) {
            FormattedFragmentGroup group = TryFindDoxygenCommandOnLinesBeforePoint(cppFileSemantics, startPoint, cCmdsToDocumentTParam);
            if (group != null && group.Fragments.Count == 2) {
              result.elementBeforeElementToPreselect = group.Fragments[1].GetText(startPoint.Snapshot);
            }
          }

          return result;
        }
        return null;
      }

      ClassOrAliasInfo clsInfo = cppFileSemantics.TryGetClassInfoIfNextIsATemplateClassOrAlias(startPoint);
      if (clsInfo != null) {
        if (clsInfo.TemplateParameters.Count() > 0) {
          var result = new AutocompleteInfoForParameterOfDoxygenCommand {
            elementsToShow = clsInfo.TemplateParameters.Select(p => (p, "")),
            parentInfo = $"{clsInfo.Type}: {clsInfo.ClassName}",
            icon = cTemplateParamImage
          };
          return result;
        }
        return null;
      }

      return null;
    }


    private ImmutableArray<CompletionItem>.Builder CreateAutocompleteItemsForParameterOfDoxygenCommand(
      AutocompleteInfoForParameterOfDoxygenCommand infos)
    {
      var itemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();
      int numParams = infos.elementsToShow.Count();
      int curParamNumber = 1;
      bool preselected = false;
      foreach ((string name, string type) in infos.elementsToShow) {
        string suffix = type != null && type != ""
          ? $"Type: {type}. {infos.parentInfo}"
          : infos.parentInfo;

        var item = new CompletionItem(
          displayText: name,
          source: this,
          icon: infos.icon,
          filters: ImmutableArray<CompletionFilter>.Empty,
          suffix: suffix,
          insertText: name,
          // As in PopulateAutcompleteBoxWithCommands(): Ensure we keep the order
          sortText: curParamNumber.ToString().PadLeft(numParams, '0'),
          filterText: name,
          automationText: name,
          attributeIcons: ImmutableArray<ImageElement>.Empty,
          commitCharacters: default,
          applicableToSpan: default,
          isCommittedAsSnippet: false,
          isPreselected: preselected);

        itemsBuilder.Add(item);
        ++curParamNumber;
        preselected = name == infos.elementBeforeElementToPreselect;
      }

      return itemsBuilder;
    }


    private bool AnyAdvancedAutocompleteEnabled() 
    {
      return mGeneralOptions.EnableAutocomplete
        && (mGeneralOptions.EnableFunctionAndMacroParameterAutocomplete || mGeneralOptions.EnableTemplateParameterAutocomplete);
    }


    // Given a text point, tries to find the next Doxygen command before that point which is in the given array 'doxygenCmds'.
    private FormattedFragmentGroup TryFindDoxygenCommandOnLinesBeforePoint(
      IVisualStudioCppFileSemantics cppFileSemantics, 
      SnapshotPoint point, 
      string[] doxygenCmds)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      // The task is to find the next Doxygen command in 'doxygenCmds' that comes before 'point. So we need to iterate backward
      // until we either find it or leave the current comment section. But how do we detect that we leave the current comment
      // section? I.e. when do we give up on searching backwards? In principle, the CommentClassifier found the start of the
      // comment, because it figured out the type of the comment ('//', '///', '/*', etc.). Problem: This is useless here,
      // because we don't need the start of the comment but rather the start of a whole comment section that the user perceives
      // as 'one unit'. A '///' comment starts before the '/'. But of course there can be comment lines above, all of them
      // indicated by '///'. So what we would need is the union of all comments that are separated by whitespaces only. Or,
      // put another way, we need to find the first C++ element before 'point' that is not a comment. This is what
      // TryGetEndPositionOfCppElementBefore() does. We then can search backward between that C++ element and 'point'.
      //
      // Unfortunately, the way the semantic information is exposed by Visual Studio, in general we cannot find the C++ element
      // before 'point' without getting **ALL** C++ elements till the start of the file. See the implementation notes in
      // TryGetEndPositionOfCppElementBefore(). Thus we need to implement some cutoff, indicated by cNumLinesBackwards.
      // 30 lines back seems like a reasonable upper bound; who writes longer parameter descriptions?
      const int cNumLinesBackwards = 30;
      int backwardsSearchStopPosition = cppFileSemantics.TryGetEndPositionOfCppElementBefore(point, cNumLinesBackwards);
      Debug.Assert(backwardsSearchStopPosition <= point.Position);

      var snapshot = point.Snapshot;
      int backwardsSearchLineNumStop = snapshot.GetLineFromPosition(backwardsSearchStopPosition).LineNumber;

      if (snapshot.TextBuffer.Properties.TryGetProperty(
          typeof(CommentClassifier), out CommentClassifier commentClassifier)) {
        // We assume only one Doxygen command per line.
        for (int lineNum = point.GetContainingLineNumber() - 1; lineNum >= backwardsSearchLineNumStop; --lineNum) {
          ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNum);

          // We use the extent **including** the line break characters (\r\n) because CommentClassifier.GetClassificationSpans()
          // is called by Visual Studio typically with lines including them, meaning that the CommentClassifier caches lines
          // including the line break characters. So, by including them here, too, the ParseSpan() method can more likely simply
          // return already cached information.
          var foundFragmentGroups = commentClassifier.ParseSpan(line.ExtentIncludingLineBreak);

          foreach (FormattedFragmentGroup group in foundFragmentGroups.Reverse()) {
            if (group.Fragments.Count > 0) {
              string classifiedCommand = group.Fragments[0].GetText(snapshot);
              if (classifiedCommand.Length > 1 && doxygenCmds.Contains(classifiedCommand.Substring(1))) {
                return group;
              }
            }
          }
        }
      }

      return null;
    }


    // http://glyphlist.azurewebsites.net/knownmonikers/
    // https://github.com/madskristensen/KnownMonikersExplorer
    private static ImageElement cCompletionImage = new ImageElement(KnownMonikers.CommentCode.ToImageId(), "Doxygen command");
    // Image for parameter and template parameter: These should be the images shown by VS's own IntelliSense in C++.
    private static ImageElement cParamImage = new ImageElement(KnownMonikers.FieldPublic.ToImageId(), "Doxygen parameter");
    private static ImageElement cTemplateParamImage = new ImageElement(KnownMonikers.TypeDefinition.ToImageId(), "Doxygen template parameter");

    private static readonly string[] cCmdsToDocumentParam = new string[] { "param", "param[in]", "param[out]", "param[in,out]" };
    private static readonly string[] cCmdsToReferToParam = new string[] { "p", "a" };
    private static readonly string[] cCmdsToDocumentTParam = new string[] { "tparam" };

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

