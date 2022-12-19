// If this is enabled, we disable the doxygen highlighting and instead highlight
// the various comment types ("//", "///", "/*", etc.). This allows easier debugging
// of the logic to detect the comment types.
// Also see the file "ManualTests_SplittingIntoComments.cpp."
//#define ENABLE_COMMENT_TYPE_DEBUGGING

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;


namespace VSDoxyHighlighter
{
  // Identifiers for the classifications. E.g., Visual Studio will use these strings as keys
  // to store the classification's configuration in the registry.
  public static class IDs
  {
    public const string ID_command = "DoxyTest3Command";
    public const string ID_parameter1 = "DoxyTest3Parameter1";
    public const string ID_parameter2 = "DoxyTest3Parameter2";
    public const string ID_title = "DoxyTest3Title";
    public const string ID_warningKeyword = "DoxyTest3Warning";
    public const string ID_noteKeyword = "DoxyTest3Note";
    public const string ID_emphasisMinor = "DoxyTest3EmphasisMinor";
    public const string ID_emphasisMajor = "DoxyTest3EmphasisMajor";
    public const string ID_strikethrough = "DoxyTest3Strikethrough";
    public const string ID_inlineCode = "DoxyTest3InlineCode";
  }



  /// <summary>
  /// Tells Visual Studio via MEF about the classifications provided by the extension.
  /// </summary>
  internal static class TestClassifierClassificationDefinition
  {
#pragma warning disable 169
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_command)]
    private static ClassificationTypeDefinition typeDefinitionForCommand;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_warningKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForWarningKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_noteKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForNoteKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_parameter1)]
    private static ClassificationTypeDefinition typeDefinitionForParameter1;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_parameter2)]
    private static ClassificationTypeDefinition typeDefinitionForParameter2;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_emphasisMinor)]
    private static ClassificationTypeDefinition typeDefinitionForEmphasisMinor;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_emphasisMajor)]
    private static ClassificationTypeDefinition typeDefinitionForEmphasisMajor;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_strikethrough)]
    private static ClassificationTypeDefinition typeDefinitionForStrikethrough;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_inlineCode)]
    private static ClassificationTypeDefinition typeDefinitionForInlineCode;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_title)]
    private static ClassificationTypeDefinition typeDefinitionForTitle;
#pragma warning restore 169
  }




  /// <summary>
  /// Factory for CommentClassifier. Automatically created and used by MEF.
  /// </summary>
  [Export(typeof(IClassifierProvider))]
  [ContentType("C/C++")]
  internal class CommentClassifierProvider : IClassifierProvider
  {
#pragma warning disable 649
    [Import]
    private IClassificationTypeRegistryService classificationRegistry;
#pragma warning restore 649

    public IClassifier GetClassifier(ITextBuffer buffer)
    {
      return buffer.Properties.GetOrCreateSingletonProperty<CommentClassifier>(
        creator: () => new CommentClassifier(this.classificationRegistry, buffer));
    }
  }



  /// <summary>
  /// Main "entry" point that is used by Visual Studio to get the format (i.e. classification)
  /// of some code span. An instance of this class is created by Visual Studio per text buffer
  /// via CommentClassifierProvider. Visual Studio then calls GetClassificationSpans() to get
  /// the classification.
  /// </summary>
  internal class CommentClassifier : IClassifier, IDisposable
  {
    internal CommentClassifier(IClassificationTypeRegistryService registry, ITextBuffer textBuffer)
    {
      mTextBuffer = textBuffer;
      mSpanSplitter = new SpanSplitter(textBuffer);
      mFormater = new CommentFormatter();

      int numFormats = Enum.GetNames(typeof(FormatType)).Length;
      mFormatTypeToClassificationType = new IClassificationType[numFormats];
      mFormatTypeToClassificationType[(uint)FormatType.Command] = registry.GetClassificationType(IDs.ID_command);
      mFormatTypeToClassificationType[(uint)FormatType.Parameter1] = registry.GetClassificationType(IDs.ID_parameter1);
      mFormatTypeToClassificationType[(uint)FormatType.Parameter2] = registry.GetClassificationType(IDs.ID_parameter2);
      mFormatTypeToClassificationType[(uint)FormatType.Title] = registry.GetClassificationType(IDs.ID_title);
      mFormatTypeToClassificationType[(uint)FormatType.Warning] = registry.GetClassificationType(IDs.ID_warningKeyword);
      mFormatTypeToClassificationType[(uint)FormatType.Note] = registry.GetClassificationType(IDs.ID_noteKeyword);
      mFormatTypeToClassificationType[(uint)FormatType.EmphasisMinor] = registry.GetClassificationType(IDs.ID_emphasisMinor);
      mFormatTypeToClassificationType[(uint)FormatType.EmphasisMajor] = registry.GetClassificationType(IDs.ID_emphasisMajor);
      mFormatTypeToClassificationType[(uint)FormatType.Strikethrough] = registry.GetClassificationType(IDs.ID_strikethrough);
      mFormatTypeToClassificationType[(uint)FormatType.InlineCode] = registry.GetClassificationType(IDs.ID_inlineCode);

      foreach (IClassificationType classificationType in mFormatTypeToClassificationType) {
        Debug.Assert(classificationType != null);
      }

      ThreadHelper.ThrowIfNotOnUIThread();
      mGeneralOptions = VSDoxyHighlighterPackage.GeneralOptions;
      mGeneralOptions.SettingsChanged += OnSettingsChanged;
    }


#pragma warning disable 67
    // We can call this event ourselves to tell Visual Studio that a certain span should be re-classified.
    // E.g. if we figured out that somewhere the opening of a comment is inserted by the user, and thus
    // all the following lines need to be re-classified.
    // However, in the moment we do not really do this. Instead we rely on the default Visual Studio tagger
    // to do the same thing. After all, if a user types e.g. a "/*", also the Visual Studio tagger needs
    // to re-classify everything to apply the proper comment color. This will also cause our
    // GetClassificationSpans() to be called again.
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67


    /// <summary>
    /// Called by Visual Studio when the given text span needs to be classified (i.e. formatted).
    /// Thus, this function searches for words to which apply syntax highlighting, and for each one 
    /// found returns a ClassificationSpan.
    /// 
    /// The function is typically called with individual lines.
    /// </summary>
    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan originalSpanToCheck)
    {
      ITextSnapshot textSnapshot = originalSpanToCheck.Snapshot;

      // The cache gets reset after every text edit. Hence, the cache optimizes scrolling through a file.
      if (mCachedVersion != textSnapshot.Version.VersionNumber) {
        mCache.Clear();
        mCachedVersion = textSnapshot.Version.VersionNumber;
      }
      else if (mCache.TryGetValue(originalSpanToCheck.Span, out var cachedClassifications)) {
        return cachedClassifications;
      }

      // First step: Identify the comment sections in the given span.
      List<CommentSpan> commentSpans = mSpanSplitter.SplitIntoComments(originalSpanToCheck);
      
      // Second step: Apply the formatting to each comment span.
      var result = new List<ClassificationSpan>();
      foreach (CommentSpan commentSpan in commentSpans) {
#if !ENABLE_COMMENT_TYPE_DEBUGGING
        if (ApplyHighlightingToCommentType(commentSpan.commentType)) {
          string codeText = textSnapshot.GetText(commentSpan.span);

          // Scan the given text for keywords and get the proper formatting for it.
          var fragmentsToFormat = mFormater.FormatText(codeText);

          // Convert the list of fragments that should be formatted to Visual Studio types.
          foreach (FormattedFragment fragment in fragmentsToFormat) {
            IClassificationType classificationType = mFormatTypeToClassificationType[(uint)fragment.Type];
            var spanToFormat = new Span(commentSpan.span.Start + fragment.StartIndex, fragment.Length);
            result.Add(new ClassificationSpan(new SnapshotSpan(textSnapshot, spanToFormat), classificationType));
          }
        }
#else
        IClassificationType classificationType = mFormatTypeToClassificationType[(uint)cCommentTypeDebugFormats[commentSpan.commentType]];
        result.Add(new ClassificationSpan(new SnapshotSpan(textSnapshot, commentSpan.span), classificationType));
#endif
      }

      mCache[originalSpanToCheck.Span] = result;
      return result;
    }


    private bool ApplyHighlightingToCommentType(CommentType type) 
    {
      switch (type) {
        case CommentType.TripleSlash:
          return mGeneralOptions.EnableInTripleSlash;
        case CommentType.DoubleSlashExclamation:
          return mGeneralOptions.EnableInDoubleSlashExclamation;
        case CommentType.DoubleSlash:
          return mGeneralOptions.EnableInDoubleSlash;
        case CommentType.SlashStarStar:
          return mGeneralOptions.EnableInSlashStarStar;
        case CommentType.SlashStarExclamation:
          return mGeneralOptions.EnableInSlashStarExclamation;
        case CommentType.SlashStar:
          return mGeneralOptions.EnableInSlashStar;
        default:
          return false;
      }
    }


    void IDisposable.Dispose()
    {
      if (mGeneralOptions != null) {
        mGeneralOptions.SettingsChanged -= OnSettingsChanged;
      }
    }


    private void OnSettingsChanged(object sender, EventArgs e)
    {
      mCache.Clear();
      mCachedVersion = -1;

      // We this function is called, the user clicked on "OK" in the options.
      // Some or our settings might or might not have changed. Regardless, we force a re-classification of the whole text.
      // This ensures that any possible changes become visible immediately.
      ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(
        new SnapshotSpan(mTextBuffer.CurrentSnapshot, new Span(0, mTextBuffer.CurrentSnapshot.Length))));
    }


    private readonly ITextBuffer mTextBuffer;
    private readonly SpanSplitter mSpanSplitter;
    private readonly CommentFormatter mFormater;
    private readonly IClassificationType[] mFormatTypeToClassificationType;
    private readonly GeneralOptionsPage mGeneralOptions;

    private Dictionary<Span, IList<ClassificationSpan>> mCache = new Dictionary<Span, IList<ClassificationSpan>>();
    private int mCachedVersion = -1;


#if ENABLE_COMMENT_TYPE_DEBUGGING
    static readonly Dictionary<CommentType, FormatType> cCommentTypeDebugFormats = new Dictionary<CommentType, FormatType> {
      { CommentType.TripleSlash, FormatType.Command },
      { CommentType.DoubleSlashExclamation, FormatType.Parameter1 },
      { CommentType.DoubleSlash, FormatType.Title },
      { CommentType.SlashStarStar, FormatType.EmphasisMinor },
      { CommentType.SlashStarExclamation, FormatType.Note },
      { CommentType.SlashStar, FormatType.InlineCode },
      { CommentType.Unknown, FormatType.Warning },
    };
#endif
  }

}
