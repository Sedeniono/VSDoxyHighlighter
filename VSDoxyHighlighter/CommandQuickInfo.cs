using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using System.Diagnostics;
using System.Collections.Generic;


namespace VSDoxyHighlighter
{
  //================================================================================
  // CommentCommandQuickInfoSourceProvider
  //================================================================================

  // Based on https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/AsyncQuickInfo
  [Export(typeof(IAsyncQuickInfoSourceProvider))]
  [Name("VSDoxyHighlighterCommentCommandQuickInfoSourceProvider")]
  [ContentType("C/C++")]
  internal sealed class CommentCommandQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
  {
    public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
      return textBuffer.Properties.GetOrCreateSingletonProperty(() => new CommentCommandAsyncQuickInfoSource(textBuffer));
    }
  }


  //================================================================================
  // CommentCommandAsyncQuickInfoSource
  //================================================================================

  // Based on https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/AsyncQuickInfo
  internal sealed class CommentCommandAsyncQuickInfoSource : IAsyncQuickInfoSource
  {
    public CommentCommandAsyncQuickInfoSource(ITextBuffer textBuffer)
    {
      mTextBuffer = textBuffer;
    }

    public void Dispose()
    {
    }


    /// <summary>
    /// Called by Visual Studio no a background thread when the user hovers over some arbitrary piece of text in the text buffer.
    /// It is then the function's job to figure out whether some specific quick info should be returned and displayed, or not.
    /// </summary>
    public /*async*/ Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
    {
      SnapshotPoint? triggerPoint = session.GetTriggerPoint(mTextBuffer.CurrentSnapshot);

      if (triggerPoint == null) {
        return Task.FromResult<QuickInfoItem>(null);
      }

      ITextSnapshotLine line = triggerPoint.Value.GetContainingLine();
        
      string lineText = line.GetText();
      int triggerIdxInText = triggerPoint.Value.Position - line.Start.Position;

      // TODO: Why not using the same machinery as GetClassificationSpans()?
      // Might make sense, to display quickinfo also for non-highlighted commands, such as "\brief" not at the start?

      if (0 <= triggerIdxInText && triggerIdxInText < lineText.Length) {
        int commandStartIdx = FindCommandStartIndexIncludingSlashOrAt(lineText, triggerIdxInText);
        if (commandStartIdx >= 0) {
          int commandEndIdx = FindCommandEndIndex(lineText, triggerIdxInText);
          if (commandEndIdx > commandStartIdx) {
            // TODO: Check whether in enabled comment type.

            string potentialCommand = lineText.Substring(commandStartIdx + 1, commandEndIdx - commandStartIdx);
            foreach (DoxygenHelpPageCommand cmd in AllDoxygenHelpPageCommands.cAmendedDoxygenCommands) {
              if (potentialCommand.StartsWith(cmd.Command)) {

                // TODO: Don't just copy & paste this from the autocomplete.
                // TODO: Need to sort by length of something like this. I.e. don't return infos for \returns if user hovers over \return
                // TODO: Retain hyperlinks and "Click here"?
                // TODO: Hyperlink to online documentation

                var runs = new List<ClassifiedTextRun>();

                runs.AddRange(ClassifiedTextElement.CreatePlainText("Info for command: ").Runs);
                runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[ClassificationEnum.Command], "\\" + cmd.Command));

                runs.AddRange(ClassifiedTextElement.CreatePlainText("\nCommand parameters: ").Runs);
                if (cmd.Parameters == "") {
                  runs.AddRange(ClassifiedTextElement.CreatePlainText("No parameters").Runs);
                }
                else {
                  runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[ClassificationEnum.Parameter2], cmd.Parameters));
                }

                runs.AddRange(ClassifiedTextElement.CreateHyperlink("\nHyperlink lineText", "Tooltip for test", () => {
                  int dummy = 0; // Called when clicked
                }).Runs);

                //runs.AddRange(ClassifiedTextElement.CreatePlainText("\n\n").Runs);

                //foreach (var fragment in cmd.Description) {
                //  if (fragment.Item1 == null) {
                //    runs.AddRange(ClassifiedTextElement.CreatePlainText(fragment.Item2).Runs);
                //  }
                //  else {
                //    runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[fragment.Item1.Value], fragment.Item2));
                //  }
                //}

                var lineSpan2 = mTextBuffer.CurrentSnapshot.CreateTrackingSpan(
                  commandStartIdx + line.Start.Position,
                  cmd.Command.Length + 1,
                  SpanTrackingMode.EdgeInclusive);

                return Task.FromResult(new QuickInfoItem(lineSpan2, new ClassifiedTextElement(runs)));
              }
            }
          }
        }
      }


      var lineNumber = triggerPoint.Value.GetContainingLine().LineNumber;
      var lineSpan = mTextBuffer.CurrentSnapshot.CreateTrackingSpan(line.Extent, SpanTrackingMode.EdgeInclusive);

      var lineNumberElm = new ContainerElement(
          ContainerElementStyle.Wrapped,
          new ImageElement(_icon),
          new ClassifiedTextElement(
              new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, "Line number: "),
              new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, $"{lineNumber + 1}")
          ));

      var dateElm = new ContainerElement(
          ContainerElementStyle.Stacked,
          lineNumberElm,
          new ClassifiedTextElement(
              new ClassifiedTextRun(PredefinedClassificationTypeNames.SymbolDefinition, "The current date: "),
              new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, DateTime.Now.ToShortDateString())
          ));

      return Task.FromResult(new QuickInfoItem(lineSpan, dateElm));
    }


    private int FindCommandStartIndexIncludingSlashOrAt(string lineText, int triggerIdx)
    {
      for (int idx = triggerIdx; idx >= 0; --idx) {
        char c = lineText[idx];
        if (c == '\\' || c == '@') {
          // TODO: What about "\\\brief"?
          // TODO: What about "\\brief"? Does this even highlight correctly?
          return idx;
        }
        else if (char.IsWhiteSpace(c)) {
          return -1;
        }
        else if (triggerIdx - idx > cMaxCommandLength) {
          return -1;
        }
      }

      return -1;
    }


    private int FindCommandEndIndex(string lineText, int triggerIdx)
    {
      for (int idx = triggerIdx; idx < lineText.Length; ++idx) {
        char c = lineText[idx];
        bool mightBeInCommand = char.IsLetter(c)
          //|| c == '(' || c == ')' || c == '[' || c == ']'
          //|| c == '{' || c == '}' || c == '\\' || c == '@' || c == '~' || c == '&' || c == '$'
          //|| c == '#' || c == '<' || c == '>' || c == '%' || c == '\"' || c == '.' || c == '='
          //|| c == ':' || c == '|' || c == '-'
          ;

        if (!mightBeInCommand) {
          return idx - 1;
        }
        else if (idx - triggerIdx > cMaxCommandLength) {
          return -1;
        }
      }

      return lineText.Length - 1;
    }

    private readonly ITextBuffer mTextBuffer;


    private static readonly ImageId _icon = KnownMonikers.AbstractCube.ToImageId();

    // Proper value, or better take it from cCommands
    private const int cMaxCommandLength = 30;
  }
}
