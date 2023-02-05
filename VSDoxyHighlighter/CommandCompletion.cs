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

namespace VSDoxyHighlighter
{
  //================================================================================
  // CommentCommandCompletionSource
  //================================================================================

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
      mGeneralOptions = VSDoxyHighlighterPackage.GeneralOptions;
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
          foreach (DoxygenHelpPageCommand cmd in cAmendedDoxygenCommands) {
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

        runs.AddRange(ClassifiedTextElement.CreatePlainText("Info for command: ").Runs);
        runs.Add(new ClassifiedTextRun(IDs.ToID[FormatType.Command], "\\" + cmd.Command));

        runs.AddRange(ClassifiedTextElement.CreatePlainText("\nCommand parameters: ").Runs);
        if (cmd.Parameters == "") {
          runs.AddRange(ClassifiedTextElement.CreatePlainText("No parameters").Runs);
        }
        else {
          runs.Add(new ClassifiedTextRun(IDs.ToID[FormatType.Parameter2], cmd.Parameters));
        }
        runs.AddRange(ClassifiedTextElement.CreatePlainText("\n\n").Runs);

        foreach (var fragment in cmd.Description) {
          if (fragment.Item1 == null) {
            runs.AddRange(ClassifiedTextElement.CreatePlainText(fragment.Item2).Runs);
          }
          else {
            runs.Add(new ClassifiedTextRun(IDs.ToID[fragment.Item1.Value], fragment.Item2));
          }
        }

        return Task.FromResult<object>(new ClassifiedTextElement(runs));
      }

      Debug.Assert(false);
      return Task.FromResult<object>("");
    }


    private bool IsLocationInEnabledCommentType(SnapshotPoint triggerLocation)
    {
      if (triggerLocation.Snapshot.TextBuffer.Properties.TryGetProperty(
                typeof(SpanSplitter), out SpanSplitter spanSplitter)) {
        CommentType? commentType = spanSplitter.GetTypeOfCommentBeforeLocation(triggerLocation);
        if (commentType != null) {
          return mGeneralOptions.IsEnabledInCommentType(commentType.Value);
        }
      }
      else {
        Debug.Assert(false);
      }

      return false;
    }


    static CommentCommandCompletionSource()
    {
      // We put some commands that are propably used often to the front of the list that appears in the autocomplete box.
      // The commands will be ordered according to the following list.
      var speciallyOrderedCommands = new List<string>() {
        "brief", "details", "note", "warning", "param", "tparam", "returns", "return", 
        "throws", "throw", "sa", "see", "ref", "p", "c", "a", "ingroup", 
      };
      cAmendedDoxygenCommands =
        DoxygenCommandsGeneratedFromHelpPage.cCommands.OrderBy(cmd => {
          int idx = speciallyOrderedCommands.IndexOf(cmd.Command);
          return idx != -1 ? idx : speciallyOrderedCommands.Count;
        }).ToList();


      // We additionally modify the list so that various options directly appears in the autocomplete box.
      // Note that when inserting multiple additional variations for one command, they must be listed here
      // in reverse order than how they should appear, since we always insert them directly after the
      // original command.
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "htmlonly", "[block]");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{full}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{directory}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{filename}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{extension}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{file}");
      cAmendedDoxygenCommands.RemoveAll(cmd => cmd.Command == "fileinfo"); // There is no \fileinfo without a parameter
     
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "param", "param[in,out]");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "param", "param[out]");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "param", "param[in]");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "example", "example{lineno}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "dontinclude", "dontinclude{lineno}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{lineno}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{doc}");
      
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "htmlinclude", "htmlinclude[block]");
      
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{doc}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{lineno}");
      
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{anchor:YOUR_ID}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{inline,anchor:YOUR_ID}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{inline}");

      foreach (string extension in CommentFormatter.cCodeFileExtensions.Reverse()) {
        InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "code", "code{." + extension + "}");
      }
    }


    private static void InsertCommandVariationAfterOriginal(List<DoxygenHelpPageCommand> commands, string originalCommand, string newCommand) 
    {
      int idx = cAmendedDoxygenCommands.FindIndex(x => x.Command == originalCommand);
      if (idx < 0) {
        throw new System.ArgumentException($"Command '{originalCommand}' not found in list of Doxygen commands.");
      }
      DoxygenHelpPageCommand original = cAmendedDoxygenCommands[idx];
      commands.Insert(idx + 1, new DoxygenHelpPageCommand(newCommand, original.Parameters, original.Description));
    }


    // For now, we simply use an existing Visual Studio image to show in the autocomplete box.
    // http://glyphlist.azurewebsites.net/knownmonikers/
    private static ImageElement cCompletionImage = new ImageElement(KnownMonikers.CommentCode.ToImageId(), "Doxygen command");

    private static readonly List<DoxygenHelpPageCommand> cAmendedDoxygenCommands;

    private readonly GeneralOptionsPage mGeneralOptions;
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

