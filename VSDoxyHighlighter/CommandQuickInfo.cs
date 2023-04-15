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
using System.Windows.Shapes;
using Microsoft.VisualStudio.Shell;
using System.Linq;
using System.Text.RegularExpressions;

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

  /// <summary>
  /// Responsible for providing the information that gets displayed in the quick info tool tip while
  /// the user hovers with the mouse over a specific piece of text.
  /// Based on https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/AsyncQuickInfo
  /// </summary>
  internal sealed class CommentCommandAsyncQuickInfoSource : IAsyncQuickInfoSource
  {
    public CommentCommandAsyncQuickInfoSource(ITextBuffer textBuffer)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      mTextBuffer = textBuffer;

      // We don't subscribe to change events of the options or the parser: A CommentCommandAsyncQuickInfoSource is
      // only relevent while a specific quick info box is shown. Every quick info box gets its own instance.
      // The user cannot really change the settings while keeping such a box open.
      mGeneralOptions = VSDoxyHighlighterPackage.GeneralOptions;
      mCommentParser = VSDoxyHighlighterPackage.CommentParser;
    }


    public void Dispose()
    {
    }


    /// <summary>
    /// Called by Visual Studio on a background thread when the user hovers over some arbitrary piece of text in the text buffer.
    /// It is then the function's job to figure out whether some specific quick info should be returned and displayed, or not.
    /// </summary>
    public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
    {
      if (!mGeneralOptions.EnableQuickInfo) {
        return null;
      }

      SnapshotPoint? triggerPoint = session.GetTriggerPoint(mTextBuffer.CurrentSnapshot);
      if (triggerPoint == null) {
        return null;
      }

      // Need to switch to the main thread for CommentClassifier. More precisely, because of Visual Studio's cpp colorer
      // (DefaultVSCppColorerImpl.TryGetTags()) which gets called by CommentClassifier. It fails to return anything if we
      // are not on the main thread.
      // An alternative would be to completely rely on the caches in the CommentClassifier and to ensure to never call the
      // VS cpp colorer from here. I think this should work in practice, since the classification is most likely triggered
      // before a quick info gets triggered here. But it still feels like a hack. Also, we would need some synchronization
      // for the caches to avoid race conditions.
      // So, for simplicity, we switch to the main thread. It most likely does not hurt performance noticeably since the
      // caches in the CommentClassifier buffer the most expensive parts. And because the quick info does not get triggered
      // hundreds of times in every second.
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

      (DoxygenHelpPageCommand helpPageInfo, FormattedFragmentGroup fragmentGroup) = TryGetHelpPageInfoForTriggerPoint(triggerPoint.Value);

      if (fragmentGroup != null && helpPageInfo != null) {
        // TODO: Retain hyperlinks and "Click here"?
        // TODO: Hyperlink to online documentation
        // TODO: What about markdown?

        // Only the first fragment can contain the Doxygen command.
        ClassificationEnum commandClassification = fragmentGroup.Fragments[0].Classification;
        var description = AllDoxygenHelpPageCommands.ConstructDescription(mCommentParser, helpPageInfo, commandClassification);

        // If the user moves away with the mouse from this tracking span, the quick info vanishes.
        var spanWhereQuickInfoIsValid = mTextBuffer.CurrentSnapshot.CreateTrackingSpan(
          fragmentGroup.StartIndex,
          fragmentGroup.Length,
          SpanTrackingMode.EdgeInclusive);

        return new QuickInfoItem(spanWhereQuickInfoIsValid, description);
      }

      return null;
    }


    private (DoxygenHelpPageCommand helpPageInfo, FormattedFragmentGroup fragmentGroup) TryGetHelpPageInfoForTriggerPoint(SnapshotPoint triggerPoint)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      if (triggerPoint.Snapshot.TextBuffer.Properties.TryGetProperty(
            typeof(CommentClassifier), out CommentClassifier commentClassifier)) {

        ITextSnapshotLine line = triggerPoint.GetContainingLine();

        // We use the extent **including** the linear break characters (\r\n) because CommentClassifier.GetClassificationSpans()
        // is called by Visual Studio typically with lines including them, meaning that the CommentClassifier caches lines
        // including the line break characters. So, by including them here, too, the ParseSpan() method can more likely simply
        // return already cached information.
        var foundFragmentGroups = commentClassifier.ParseSpan(line.ExtentIncludingLineBreak);

        foreach (FormattedFragmentGroup group in foundFragmentGroups) {
          // Note: We check whether the trigger point is anywhere in the whole group. I.e. if the mouse cursor hovers over
          // a parameter of a Doxygen command, we want to show the information for the command.
          // Note: The indices are already absolute (i.e. relative to the start of the text buffer).
          if (group.StartIndex <= triggerPoint.Position && triggerPoint.Position <= group.EndIndex && group.Fragments.Count > 0) {
            // Only the first fragment can contain the Doxygen command.
            FormattedFragment fragmentWithCommand = group.Fragments[0];
            string potentialCmdWithSlashOrAt = triggerPoint.Snapshot.GetText(
              fragmentWithCommand.StartIndex, fragmentWithCommand.EndIndex - fragmentWithCommand.StartIndex + 1);
            DoxygenHelpPageCommand helpPageCmd = TryFindHelpPageCommand(potentialCmdWithSlashOrAt);
            if (helpPageCmd != null) {
              return (helpPageCmd, group);
            }
          }
        }
      }

      return (null, null);
    }


    private DoxygenHelpPageCommand TryFindHelpPageCommand(string potentialCmdWithSlashOrAt)
    {
      if (potentialCmdWithSlashOrAt.Length <= 0) {
        return null;
      }
      char startChar = potentialCmdWithSlashOrAt[0];
      string potentialCmdWithoutSlash = (startChar == '\\' || startChar == '@') ? potentialCmdWithSlashOrAt.Substring(1) : potentialCmdWithSlashOrAt;

      DoxygenHelpPageCommand foundCmd = AllDoxygenHelpPageCommands.cAmendedDoxygenCommands.Find(cmd => cmd.Command == potentialCmdWithoutSlash);
      return foundCmd;
    }


    private readonly ITextBuffer mTextBuffer; 
    private readonly IGeneralOptions mGeneralOptions;
    private readonly CommentParser mCommentParser;
  }
}
