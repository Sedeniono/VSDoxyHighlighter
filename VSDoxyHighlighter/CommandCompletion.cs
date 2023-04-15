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
using static Microsoft.VisualStudio.Shell.ThreadedWaitDialogHelper;
using System.Windows.Documents;

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

    /// <summary>
    /// More or less salled by VS whenever the user created a new view of some document and starts typing in there.
    /// </summary>
    public IAsyncCompletionSource GetOrCreate(ITextView textView)
    {
      if (mCache.TryGetValue(textView, out var itemSource))
        return itemSource;

      var source = new CommentCommandCompletionSource();
      textView.Closed += (o, e) => mCache.Remove(textView);
      mCache.Add(textView, source);
      return source;
    }
  }


  /// <summary>
  /// Defines when the autocomplete box should appear as well as its content.
  /// Based on https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/AsyncCompletion
  /// </summary>
  class CommentCommandCompletionSource : IAsyncCompletionSource
  {
    public CommentCommandCompletionSource() 
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      // We don't subscribe to change events of the options or the parser: A CommentCommandCompletionSource is
      // only relevent while a specific autocomplete box is shown. Every autocomplete box gets its own instance.
      // The user cannot really change the settings while keeping such a box open.
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
        // Check whether we hit the start of a text fragment that might be doxygen command.
        if (c == '\\' || c == '@') {
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
            return new CompletionStartData(
              CompletionParticipation.ProvidesItems,
              applicableToSpan);
          }
        }
        // We stop looking at the beginning of the line or at a whitespace; we only support the autocompletion
        // of the actual commands at the moment, not of their arguments.
        else if (c == '\n' || c == '\r' ||c == ' ' || c == '\t') {
          return CompletionStartData.DoesNotParticipateInCompletion;
        }
      }

      return CompletionStartData.DoesNotParticipateInCompletion;
    }


    /// <summary>
    /// Called by VS once per completion session on a background thread to fetch the set of all completion 
    /// items available at the given location.
    /// </summary>
    public /*async*/ Task<CompletionContext> GetCompletionContextAsync(
      IAsyncCompletionSession session, 
      CompletionTrigger trigger, 
      SnapshotPoint triggerLocation, 
      SnapshotSpan applicableToSpan, 
      CancellationToken token)
    {
      if (!mGeneralOptions.EnableAutocomplete) {
        return Task.FromResult(CompletionContext.Empty);
      }

      var itemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();
      if (applicableToSpan.Start.Position > 0) {
        char startChar = applicableToSpan.Start.Subtract(1).GetChar();
        if (startChar == '\\' || startChar == '@') {
          int numCommands = DoxygenCommandsGeneratedFromHelpPage.cCommands.Length;
          int curCommandNumber = 1;

          // TODO: Cache the items? There can only be two versions, one with @ and one with "\"?
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

            // Add a reference to the DoxygenHelpPageCommand to the item so that we access it in GetDescriptionAsync().
            item.Properties.AddProperty(typeof(DoxygenHelpPageCommand), cmd);

            itemsBuilder.Add(item);

            ++curCommandNumber;
          }
        }
      }

      return Task.FromResult(new CompletionContext(itemsBuilder.ToImmutable(), null));
    }


    /// <summary>
    /// Called by VS on a background thread to get the tooltip description for the specific <paramref name="item"/>.
    /// </summary>
    public /*async*/ Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
    {
      if (item.Properties.TryGetProperty(typeof(DoxygenHelpPageCommand), out DoxygenHelpPageCommand cmd)) {
        var runs = new List<ClassifiedTextRun>();

        //------ Add a line with the actual command.
        string cmdWithSlash = "\\" + cmd.Command;
        ClassificationEnum commandClassification = GetClassificationForCommand(cmdWithSlash);
        runs.AddRange(ClassifiedTextElement.CreatePlainText("Info for command: ").Runs);
        runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[commandClassification], cmdWithSlash));

        //------ Add a line with the command's parameters.
        runs.AddRange(ClassifiedTextElement.CreatePlainText("\nCommand parameters: ").Runs);
        if (cmd.Parameters == "") {
          runs.AddRange(ClassifiedTextElement.CreatePlainText("No parameters").Runs);
        }
        else {
          // Using "Parameter2" since, by default, it is displayed non-bold, causing a nicer display.
          runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[ClassificationEnum.Parameter2], cmd.Parameters));
        }
        runs.AddRange(ClassifiedTextElement.CreatePlainText("\n\n").Runs);

        //------ Add the whole description.
        foreach (var descriptionFragment in cmd.Description) {
          AddTextRunsForDescriptionFragment(descriptionFragment, runs);
        }

        return Task.FromResult<object>(new ClassifiedTextElement(runs));
      }

      Debug.Assert(false);
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


    private ClassificationEnum GetClassificationForCommand(string cmdWithSlash)
    {
      Debug.Assert(cmdWithSlash.StartsWith("\\") || cmdWithSlash.StartsWith("@"));

      var parsed = mCommentParser.Parse(cmdWithSlash);
      if (parsed.Count() == 1) {
        FormattedFragmentGroup group = parsed.First();
        if (group.Fragments.Count == 1) {
          return group.Fragments[0].Classification;
        }
      }
      
      Debug.Assert(false);
      return ClassificationEnum.Command;
    }


    private void AddTextRunsForDescriptionFragment((object, string) descriptionFragment, List<ClassifiedTextRun> outputList)
    {
      if (descriptionFragment.Item1 is ClassificationEnum classification) {
        // Use the given classification as-is.
        outputList.Add(new ClassifiedTextRun(ClassificationIDs.ToID[classification], descriptionFragment.Item2));
        return;
      }

      if (descriptionFragment.Item1 is DoxygenHelpPageCommand.OtherTypesEnum otherType) {
        switch (otherType) {
          case DoxygenHelpPageCommand.OtherTypesEnum.Command:
            ClassificationEnum classificationForOther = GetClassificationForCommand(descriptionFragment.Item2);
            outputList.Add(new ClassifiedTextRun(ClassificationIDs.ToID[classificationForOther], descriptionFragment.Item2));
            return;
          default:
            throw new VSDoxyHighlighterException($"Unknown value for DoxygenHelpPageCommand.OtherTypesEnum: {otherType}");
        }
      }

      outputList.AddRange(ClassifiedTextElement.CreatePlainText(descriptionFragment.Item2).Runs);
    }


    // For now, we simply use an existing Visual Studio image to show in the autocomplete box.
    // http://glyphlist.azurewebsites.net/knownmonikers/
    private static ImageElement cCompletionImage = new ImageElement(KnownMonikers.CommentCode.ToImageId(), "Doxygen command");

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
      if (mCache.TryGetValue(textView, out var itemSource))
        return itemSource;

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

