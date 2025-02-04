﻿using Microsoft.VisualStudio.Core.Imaging;
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
using System.Text.RegularExpressions;


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
    public enum CompletionTarget
    { 
      Command, // The Doxygen command itself
      Parameter // A parameter of the Doxygen command
    }


    public static readonly ImmutableArray<char> cPunctuationChars = new char[] { ',', ';', ':', '.', '!', '?' }.ToImmutableArray();


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
        // We don't trigger a *new* autocompletion session for punctuation characters. Intention: When typing e.g.
        //   "/// See the \p point. More text after sentence"
        // we don't want to trigger the completion when the user types the "." after the "point".
        else if (cPunctuationChars.Contains(c)) {
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
      CompletionTarget? completionTarget = null;
      if (applicableToSpan.Start.Position > 0) {
        SnapshotPoint startPoint = applicableToSpan.Start.Subtract(1);
        char startChar = startPoint.GetChar();
        // '\' and '@' start a command.
        if (startChar == '\\' || startChar == '@') {
          itemsBuilder = PopulateAutcompleteBoxWithCommands(startChar);
          completionTarget = CompletionTarget.Command;
        }
        // If the user typed a whitespace, we check whether it happend after a Doxygen command for which we
        // support autocompletion of the command's parameter, and if yes, populate the autocomplete box with possible parameter values.
        else if (AnyAdvancedAutocompleteEnabled() && (startChar == ' ' || startChar == '\t')) {
          try {
            itemsBuilder = await PopulateAutocompleteBoxForParameterOfDoxygenCommandAsync(startPoint, cancellationToken);
            completionTarget = CompletionTarget.Parameter;
          }
          catch (Exception ex) {
            ActivityLog.LogError("VSDoxyHighlighter", $"Exception occurred while checking for parameter completion: {ex}");
          }
        }
      }

      if (itemsBuilder != null) {
        Debug.Assert(completionTarget != null);
        session.Properties.AddProperty(typeof(CompletionTarget), completionTarget.Value);
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
      else if (item.Properties.TryGetProperty(typeof(ParameterAutocompleteSingleEntry), out ParameterAutocompleteSingleEntry singleEntry)) {
        string description = "";
        if (!string.IsNullOrEmpty(singleEntry.type)) {
          description += $"Type: {singleEntry.type}\n";
        }
        description += singleEntry.context;
        return Task.FromResult<object>(description);
      }

      return Task.FromResult<object>("");
    }


    private bool IsLocationInEnabledCommentType(SnapshotPoint triggerLocation)
    {
      // Exploit the existing CommentClassifier associated with the text buffer to figure out whether
      // the location is in a comment type where autocomplete should be enabled. 
      CommentClassifier commentClassifier = TryGetCommentClassifier(triggerLocation.Snapshot.TextBuffer);
      if (commentClassifier != null) {
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


    CommentClassifier TryGetCommentClassifier(ITextBuffer textBuffer)
    {
      // The CommentClassifier got created by Visual Studio via CommentClassifierProvider.GetClassifier() when the text
      // buffer got created.
      if (textBuffer != null && 
          textBuffer.Properties.TryGetProperty(typeof(CommentClassifier), out CommentClassifier commentClassifier)) {
        return commentClassifier;
      }
      return null;
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
      var infos = await TryGetAutocompleteInfosForParameterOfDoxygenCommandAsync(startPoint, cancellationToken);
      if (infos != null && infos.Count > 0) {
        return CreateAutocompleteItemsForParameterOfDoxygenCommand(infos);
      }
      return null;
    }


    // Represents a single entry that appears in the autocomplete box when autocompleting a parameter of a Doxygen command.
    private class ParameterAutocompleteSingleEntry
    {
      public string name; // Name of the C++ element (e.g. of the template parameter)
      public string type; // Type of the C++ element, if available (e.g. "const int"). Show on the right side of the autocomplete box.
      public string context; // Information about the C++ element that contains the entry (e.g. the class, function, etc.). Shown as tooltip.
      public ImageElement icon;
    }


    // Entries for the autocomplete box associated with a single code element (function, class, etc.).
    private class AutocompleteInfosForParameterOfDoxygenCommand
    {
      public IEnumerable<ParameterAutocompleteSingleEntry> elementsToShow;
            
      // If an element in 'elementsToShow' matches this string, the next one after that will be preselected.
      // This is used to make writing parameter documentation more convenient. For example, if one is successively documenting
      // the function parameters with '@param', the autocomplete box will preselect the most likely next parameter one wants
      // to insert, i.e. the one which comes next after the previously documented parameter.
      public string elementBeforeElementToPreselect;
    }


    private async Task<IList<AutocompleteInfosForParameterOfDoxygenCommand>> TryGetAutocompleteInfosForParameterOfDoxygenCommandAsync(
      SnapshotPoint startPoint, CancellationToken cancellationToken)
    {
      // Get the Doxygen command without the "\" or "@".
      string command = TryGetDoxygenCommandBeforeWhitespace(startPoint);
      if (command == null) {
        return null;
      }

      // Check for "@param"
      Match paramMatch = cCmdToDocumentParamRegex.Match(command);
      if (paramMatch.Success) {
        if (mGeneralOptions.EnableParameterAutocompleteFor_param) {
          var infos = await TryGetParametersOfNextCodeElementAsync(startPoint, cCmdsToDocumentParam, preselectAfterPrevious: true, cancellationToken);
          if (infos == null) {
            infos = new AutocompleteInfosForParameterOfDoxygenCommand {
              elementsToShow = new List<ParameterAutocompleteSingleEntry>()
            };
          }

          // If the user has not already typed in the optional "[in,out]" part, add them to the autocomplete list.
          // We add them to the end of the list because we guess that they are used not very often.
          Debug.Assert(paramMatch.Groups.Count == 2);
          bool containsBrackets = paramMatch.Groups[1].Success;
          if (!containsBrackets) {
            const string cDirContext = "Optional <dir> attribute";
            infos.elementsToShow = infos.elementsToShow.Concat(
              new[] {
                  new ParameterAutocompleteSingleEntry() { name = "[in]", context = cDirContext, icon = cParamInOutImage },
                  new ParameterAutocompleteSingleEntry() { name = "[out]", context = cDirContext, icon = cParamInOutImage },
                  new ParameterAutocompleteSingleEntry() { name = "[in,out]", context = cDirContext, icon = cParamInOutImage },
                  new ParameterAutocompleteSingleEntry() { name = "[inout]", context = cDirContext, icon = cParamInOutImage },
              });
          }

          return new List<AutocompleteInfosForParameterOfDoxygenCommand>() { infos };
        }
        return null;
      }

      // Check for "@tparam"
      if (cCmdsToDocumentTParam.Contains(command)) {
        if (mGeneralOptions.EnableParameterAutocompleteFor_tparam) {
          var infos = await TryGetTemplateParametersOfNextCodeElementAsync(startPoint, cCmdsToDocumentTParam, preselectAfterPrevious: true, cancellationToken);
          return infos != null ? new List<AutocompleteInfosForParameterOfDoxygenCommand>() { infos } : null;
        }
        return null;
      }

      // Check for "@p" and "@a".
      if (mGeneralOptions.EnableParameterAutocompleteFor_p_a) {
        if (cCmdsToReferToParam.Contains(command)) {
          // Note: preselectAfterPrevious = false. We cannot sensibly guess which parameters the user would want
          // to have preselected for "@p" and "@a".
          var normalParamsInfo = await TryGetParametersOfNextCodeElementAsync(startPoint, cCmdsToReferToParam, preselectAfterPrevious: false, cancellationToken);
          var templateParamsInfo = await TryGetTemplateParametersOfNextCodeElementAsync(startPoint, cCmdsToReferToParam, preselectAfterPrevious: false, cancellationToken);
          var result = new List<AutocompleteInfosForParameterOfDoxygenCommand>();
          if (normalParamsInfo != null) {
            result.Add(normalParamsInfo);
          }
          if (templateParamsInfo != null) {
            result.Add(templateParamsInfo);
          }
          return result;
        }
        return null;
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


    private async Task<AutocompleteInfosForParameterOfDoxygenCommand> TryGetParametersOfNextCodeElementAsync(
      SnapshotPoint startPoint, string[] doxygenCmds, bool preselectAfterPrevious, CancellationToken cancellationToken) 
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

      // We first check for a macro and then for a function, not vice versa. Reason: Assume there is a macro
      // definition and then a function. Moreover, assume that we try to autocomplete for a "@param" above
      // the macro definition. If we did check for a function here first, we would find the function and thus
      // show the function's parameters rather than the macro's parameters. That we would find the function
      // is an unfortunate consequence of the hacks in CppFileSemantics to cope with Visual Studio's broken
      // FileCodeModel, which are very hard to get right. (Essentially, a function declaration can contain
      // elements whose semantic token are macros, and this cannot be properly distinguished from a macro
      // definition). So hack around the issue here by checking for a macro first, and for a function second.
      // This especially works well because a macro definition never contains function-like semantic tokens.
      MacroInfo macroInfo = cppFileSemantics.TryGetMacroInfoIfNextIsAMacro(startPoint);
      if (macroInfo != null) {
        string context = $"Parameter of macro: {macroInfo.MacroName}";
        var result = new AutocompleteInfosForParameterOfDoxygenCommand {
          elementsToShow = macroInfo.Parameters.Select(
            p => new ParameterAutocompleteSingleEntry() { name = p, context = context, icon = cParamImage }),
        };
        if (preselectAfterPrevious) {
          result.elementBeforeElementToPreselect
            = TryFindParameterReferencingCodeOfPreviousDoxygenCommand(cppFileSemantics, startPoint, doxygenCmds);
        }
        return result;
      }

      FunctionInfo funcInfo = cppFileSemantics.TryGetFunctionInfoIfNextIsAFunction(startPoint);
      if (funcInfo != null) {
        string context = $"Parameter of function: {funcInfo.FunctionName}";
        var result = new AutocompleteInfosForParameterOfDoxygenCommand {
          elementsToShow = funcInfo.Parameters.Select(
            p => new ParameterAutocompleteSingleEntry() { name = p.Name, type = p.Type, context = context, icon = cParamImage }),
        };
        if (preselectAfterPrevious) {
          result.elementBeforeElementToPreselect 
            = TryFindParameterReferencingCodeOfPreviousDoxygenCommand(cppFileSemantics, startPoint, doxygenCmds);
        }
        return result;
      }

      return null;
    }


    private async Task<AutocompleteInfosForParameterOfDoxygenCommand> TryGetTemplateParametersOfNextCodeElementAsync(
      SnapshotPoint startPoint, string[] doxygenCmds, bool preselectAfterPrevious, CancellationToken cancellationToken)
    {
      // As in TryGetParametersOfNextCodeElementAsync(): Switch to the main thread for the FileCodeModel, especially because of performance.
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

      var cppFileSemantics = new CppFileSemanticsFromVSCodeModelAndCache(mAdapterService, mTextView.TextBuffer);

      FunctionInfo funcInfo = cppFileSemantics.TryGetFunctionInfoIfNextIsAFunction(startPoint);
      if (funcInfo != null) {
        if (funcInfo.TemplateParameters.Count() > 0) {
          string context = $"Template parameter of function: {funcInfo.FunctionName}";
          var result = new AutocompleteInfosForParameterOfDoxygenCommand {
            elementsToShow = funcInfo.TemplateParameters.Select(
              p => new ParameterAutocompleteSingleEntry() { name = p, context = context, icon = cTemplateParamImage }),
          };

          if (preselectAfterPrevious) {
            result.elementBeforeElementToPreselect
              = TryFindParameterReferencingCodeOfPreviousDoxygenCommand(cppFileSemantics, startPoint, doxygenCmds);
          }

          return result;
        }
        return null;
      }

      ClassOrAliasInfo clsInfo = cppFileSemantics.TryGetClassInfoIfNextIsATemplateClassOrAlias(startPoint);
      if (clsInfo != null) {
        if (clsInfo.TemplateParameters.Count() > 0) {
          string context = $"Template parameter of {clsInfo.Type.ToLower()}: {clsInfo.ClassName}";
          var result = new AutocompleteInfosForParameterOfDoxygenCommand {
            elementsToShow = clsInfo.TemplateParameters.Select(
              p => new ParameterAutocompleteSingleEntry() { name = p, context = context, icon = cTemplateParamImage }),
          }; 

          if (preselectAfterPrevious) {
            result.elementBeforeElementToPreselect
              = TryFindParameterReferencingCodeOfPreviousDoxygenCommand(cppFileSemantics, startPoint, doxygenCmds);
          }

          return result;
        }
        return null;
      }

      return null;
    }


    private ImmutableArray<CompletionItem>.Builder CreateAutocompleteItemsForParameterOfDoxygenCommand(
      IList<AutocompleteInfosForParameterOfDoxygenCommand> infos)
    {
      var itemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();
      int numParams = infos.Sum(i => i.elementsToShow.Count());
      int curParamNumber = 1;
      bool preselected = false;
      foreach (var info in infos) {
        foreach (ParameterAutocompleteSingleEntry singleEntry in info.elementsToShow) {
          string name = singleEntry.name;

          var item = new CompletionItem(
            displayText: name,
            source: this,
            icon: singleEntry.icon,
            filters: ImmutableArray<CompletionFilter>.Empty,
            suffix: singleEntry.type,
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

          // Add a reference to the DoxygenHelpPageCommand to the item so that we can access it in GetDescriptionAsync().
          item.Properties.AddProperty(typeof(ParameterAutocompleteSingleEntry), singleEntry);

          itemsBuilder.Add(item);
          ++curParamNumber;
          preselected = name == info.elementBeforeElementToPreselect;
        }
      }

      return itemsBuilder;
    }


    private bool AnyAdvancedAutocompleteEnabled() 
    {
      return mGeneralOptions.EnableAutocomplete
        && (mGeneralOptions.EnableParameterAutocompleteFor_param || mGeneralOptions.EnableParameterAutocompleteFor_tparam
            || mGeneralOptions.EnableParameterAutocompleteFor_p_a);
    }


    // When autocompleting the argument of "@param", "@param[in]", "@tparam" etc. which references a C++ function parameter
    // or a C++ template parameter, we want to check the previous occurence of "@param" etc, and find the C++ parameter
    // of that last "@param". This function returns that last C++ parameter. We use that info to preselect the next C++
    // parameter in the current autocomplete box.
    // For example, assume we have
    //    /// @param[in] param2 Parameter 2
    //    /// @param 
    //    void func(int param1, int param2, int param3);
    // where the user typed the second "@param" and hits space. We then want to open the autocomplete box and preselect "param3".
    // To this end, we need to figure out that the previous "@param" command was for "param2". This function here tries to
    // find and returns "param2".
    private string TryFindParameterReferencingCodeOfPreviousDoxygenCommand(
      IVisualStudioCppFileSemantics cppFileSemantics,
      SnapshotPoint point,
      string[] doxygenCmds)
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      FormattedFragmentGroup group = TryFindPreviousDoxygenCommandOnLinesBeforePoint(cppFileSemantics, point, doxygenCmds);
      if (group != null && group.Fragments.Count >= 2) {
        // Note: We exploit that this function here is called only in the context of Doxygen commands where the relevant
        // code-referencing parameter is always the last one. (I.e. true for "@p", "@param[in]", "@tparam", etc.)
        return group.Fragments.Last().GetText(point.Snapshot);
      }
      return null;
    }


    // Given a text point, tries to find the next Doxygen command before that point which is in the given array 'doxygenCmds'.
    private FormattedFragmentGroup TryFindPreviousDoxygenCommandOnLinesBeforePoint(
      IVisualStudioCppFileSemantics cppFileSemantics, 
      SnapshotPoint point, 
      string[] doxygenCmds)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      // The task is to find the Doxygen command in 'doxygenCmds' that comes before 'point. So we need to iterate backward
      // until we either find it or leave the current comment section. But how do we detect that we leave the current comment
      // section? I.e. when do we give up on searching backwards? In principle, the CommentClassifier found the start of the
      // comment, because it figured out the type of the comment ('//', '///', '/*', etc.). Problem: This is useless here,
      // because we don't need the start of the comment but rather the start of a whole comment section that the user perceives
      // as 'one unit'. A '///' comment starts at the first '/'. But of course there can be comment lines above, all of them
      // indicated by '///'. So what we would need is the union of all comments that are separated by whitespaces only. Or,
      // put another way, we need to find the first C++ element before 'point' that is not a comment. This is what
      // TryGetEndPositionOfCppElementBefore() does. We then can search backward between 'point' and that C++ element.
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

      CommentClassifier commentClassifier = TryGetCommentClassifier(snapshot.TextBuffer);
      if (commentClassifier != null) {
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
    private static ImageElement cParamInOutImage = new ImageElement(KnownMonikers.AzureResourceGroup.ToImageId(), "Doxygen in-out argument");
    // Image for parameter and template parameter: These should be the images shown by VS's own IntelliSense in C++.
    private static ImageElement cParamImage = new ImageElement(KnownMonikers.FieldPublic.ToImageId(), "Doxygen parameter");
    private static ImageElement cTemplateParamImage = new ImageElement(KnownMonikers.TypeDefinition.ToImageId(), "Doxygen template parameter");

    // For "@param[in,out]": Matches if the string starts with "param" and if afterwards optionally the "[in,out]" part comes,
    // and if then the string ends (except for whitespace). So matches e.g. "param [ in]" but not "param [ in] ParamName".
    // Also compare BuildRegex_ParamCommand().
    private static readonly Regex cCmdToDocumentParamRegex = new Regex(@"^param[ \t]*(\[[ \t]*(?:in|out|in[ \t]*,[ \t]*out|out[ \t]*,[ \t]*in)[ \t]*\])?(?:[ \t])*$", RegexOptions.Compiled);
    private static readonly string[] cCmdsToDocumentParam = new string[] { "param" };

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
    // These are the **potential** commit characters. VS will call ShouldCommitCompletion() only when the user typed them.
    private static ImmutableArray<char> cPotentialCommitChars = CommentCommandCompletionSource.cPunctuationChars.Insert(0, ' ');
    public IEnumerable<char> PotentialCommitCharacters => cPotentialCommitChars;

    public bool ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
    {
      // When the user hits tab, enter or double-clicks, it inserts the current selection in the autocomplete box.
      // Additionally, we also do it when the user types a space. This especially ensures that in case the user
      // typed the command completely, the autocomplete box vanishes instead of staying up.
      if (typedChar == ' ') {
        return true;
      }

      // If we are completing a parameter of a Doxygen command, we commit if the user types a punctuation character.
      // This ensures that when typing e.g.
      //   "/// See the \p point. More text after sentence"
      // the completion ends when the user types the "." after "point".
      if (CommentCommandCompletionSource.cPunctuationChars.Contains(typedChar)) {
        if (session.Properties.TryGetProperty(typeof(CommentCommandCompletionSource.CompletionTarget), 
                                            out CommentCommandCompletionSource.CompletionTarget target)) {
          if (target == CommentCommandCompletionSource.CompletionTarget.Parameter) {
            return true;
          }
        }
        return false;
      }

      return true;
    }

    public CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
    {
      return CommitResult.Unhandled; // Rely on the default VS behavior
    }
  }
}

