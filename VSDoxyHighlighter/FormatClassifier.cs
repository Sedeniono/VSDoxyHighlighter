// If this #define is enabled, we disable the doxygen highlighting and instead highlight
// the various comment types ("//", "///", "/*", etc.). This allows easier debugging
// of the logic to detect the comment types.
// Also see the file "ManualTests_SplittingIntoComments.cpp."
//
//#define ENABLE_COMMENT_TYPE_DEBUGGING

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using System.Linq;

namespace VSDoxyHighlighter
{
  // Enumeration of all possible classifications. We could have also used the string IDs, but an enum is more convenient
  // (e.g. to find all occurrences, or to get shorter serialized values).
  // NOTE: The values are serialized in the options page! Also, the values are used as indices!
  public enum ClassificationEnum : uint
  {
    Command = 0,
    Note = 1,
    Warning = 2,
    Exceptions = 3,
    Parameter1 = 4,
    Parameter2 = 5,
    Title = 6,
    EmphasisMinor = 7,
    EmphasisMajor = 8,
    Strikethrough = 9,
    InlineCode = 10,
    // The following are not used by default. But they exist so that the user can configure some
    // commands or parameters to use themto have some additional colors.
    Generic1 = 11,
    Generic2 = 12,
    Generic3 = 13,
    Generic4 = 14,
    Generic5 = 15
  }


  /// <summary>
  /// Identifiers for the classifications. E.g., Visual Studio will use these strings as keys
  /// to store the classification's configuration in the registry.
  /// </summary>
  public static class ClassificationIDs
  {
    public const string ID_command = "VSDoxyHighlighter_Command";
    public const string ID_parameter1 = "VSDoxyHighlighter_Parameter1";
    public const string ID_parameter2 = "VSDoxyHighlighter_Parameter2";
    public const string ID_title = "VSDoxyHighlighter_Title";
    public const string ID_warningKeyword = "VSDoxyHighlighter_Warning";
    public const string ID_noteKeyword = "VSDoxyHighlighter_Note";
    public const string ID_exceptions = "VSDoxyHighlighter_Exceptions";
    public const string ID_emphasisMinor = "VSDoxyHighlighter_EmphasisMinor";
    public const string ID_emphasisMajor = "VSDoxyHighlighter_EmphasisMajor";
    public const string ID_strikethrough = "VSDoxyHighlighter_Strikethrough";
    public const string ID_inlineCode = "VSDoxyHighlighter_InlineCode";
    public const string ID_generic1 = "VSDoxyHighlighter_Generic1";
    public const string ID_generic2 = "VSDoxyHighlighter_Generic2";
    public const string ID_generic3 = "VSDoxyHighlighter_Generic3";
    public const string ID_generic4 = "VSDoxyHighlighter_Generic4";
    public const string ID_generic5 = "VSDoxyHighlighter_Generic5";

    public static readonly Dictionary<ClassificationEnum, string> ToID = new Dictionary<ClassificationEnum, string>(){
        {ClassificationEnum.Command, ID_command},
        {ClassificationEnum.Parameter1, ID_parameter1},
        {ClassificationEnum.Parameter2, ID_parameter2},
        {ClassificationEnum.Title, ID_title},
        {ClassificationEnum.Warning, ID_warningKeyword},
        {ClassificationEnum.Note, ID_noteKeyword},
        {ClassificationEnum.Exceptions, ID_exceptions},
        {ClassificationEnum.EmphasisMinor, ID_emphasisMinor},
        {ClassificationEnum.EmphasisMajor, ID_emphasisMajor},
        {ClassificationEnum.Strikethrough, ID_strikethrough},
        {ClassificationEnum.InlineCode, ID_inlineCode},
        {ClassificationEnum.Generic1, ID_generic1},
        {ClassificationEnum.Generic2, ID_generic2},
        {ClassificationEnum.Generic3, ID_generic3},
        {ClassificationEnum.Generic4, ID_generic4},
        {ClassificationEnum.Generic5, ID_generic5},
      };
  }






  /// <summary>
  /// Tells Visual Studio via MEF about the classifications provided by the extension.
  /// </summary>
  internal static class CommentClassificationDefinitions
  {
#pragma warning disable 169
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_command)]
    private static ClassificationTypeDefinition typeDefinitionForCommand;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_warningKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForWarningKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_noteKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForNoteKeyword;
    
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_exceptions)]
    private static ClassificationTypeDefinition typeDefinitionForExceptions;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_parameter1)]
    private static ClassificationTypeDefinition typeDefinitionForParameter1;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_parameter2)]
    private static ClassificationTypeDefinition typeDefinitionForParameter2;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_emphasisMinor)]
    private static ClassificationTypeDefinition typeDefinitionForEmphasisMinor;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_emphasisMajor)]
    private static ClassificationTypeDefinition typeDefinitionForEmphasisMajor;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_strikethrough)]
    private static ClassificationTypeDefinition typeDefinitionForStrikethrough;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_inlineCode)]
    private static ClassificationTypeDefinition typeDefinitionForInlineCode;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_title)]
    private static ClassificationTypeDefinition typeDefinitionForTitle;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic1)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric1;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic2)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric2;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic3)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric3;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic4)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric4;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic5)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric5;
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

      mVSCppColorer = new DefaultVSCppColorerImpl(textBuffer);
      mVSCppColorer.CppColorerReclassifiedSpan += OnVSCppColorerReclassifiedSpan;

      mSpanSplitter = new SpanSplitter(mVSCppColorer);

      int numClassifications = Enum.GetNames(typeof(ClassificationEnum)).Length;
      Debug.Assert(numClassifications == ClassificationIDs.ToID.Count);
      mClassificationEnumToInstance = new IClassificationType[numClassifications];
      foreach (var enumAndID in ClassificationIDs.ToID) {
        IClassificationType classificationType = registry.GetClassificationType(enumAndID.Value);
        Debug.Assert(classificationType != null);
        mClassificationEnumToInstance[(uint)enumAndID.Key] = classificationType;
      }

      ThreadHelper.ThrowIfNotOnUIThread();
      mGeneralOptions = VSDoxyHighlighterPackage.GeneralOptions;
      mGeneralOptions.SettingsChanged += OnSettingsOrCommandsChanged;

      mParser = VSDoxyHighlighterPackage.CommentParser;
      mParser.ParsingMethodChanged += OnSettingsOrCommandsChanged;
    }


    // We can call this event ourselves to tell Visual Studio that a certain span should be re-classified.
    // E.g. if we figured out that somewhere the opening of a comment is inserted by the user, and thus
    // all the following lines need to be re-classified.
    // However, in the moment we do not really do this. Instead we rely on the default Visual Studio tagger
    // to do the same thing. After all, if a user types e.g. a "/*", also the Visual Studio tagger needs
    // to re-classify everything to apply the proper comment color. This will also cause our
    // GetClassificationSpans() to be called again.
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;


    /// <summary>
    /// Called by Visual Studio when the given text span needs to be classified (i.e. formatted).
    /// Thus, this function searches for words to which apply syntax highlighting, and for each one 
    /// found returns a ClassificationSpan.
    /// 
    /// The function is typically called by VS with individual lines.
    /// </summary>
    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan originalSpanToCheck)
    {
      mVSCppColorer.InitializeLazily(); // Ensure the events are set up properly.

      if (!mGeneralOptions.EnableHighlighting) {
        return new List<ClassificationSpan>();
      }

      ITextSnapshot textSnapshot = originalSpanToCheck.Snapshot;

      // Performance optimization: GetClassificationSpans() gets called several times after every edit.
      // 1) Once by VS itself.
      // 2) Then the Visual Studio cpp colorer triggers a reclassification a short time later, after it updated its internal
      //    view of the document (where a comment starts, etc.). Actually, we do want to reclassify the text again in this case
      //    since we rely on the VS cpp colorer. See OnVSCppColorerReclassifiedSpan() for this.
      // 3) Afterwards, several calls due to the outlining feature of Visual Studio.
      // 4) Extensions such as Viasfora create aggregators via IClassifierAggregatorService, IBufferTagAggregatorFactoryService, etc,
      //    and when they request the classifications from these aggregators, our code gets triggered again.
      // 5) Scrolling in the text documents also constantly triggers GetClassificationSpans() calls.
      // ==> We optimize (3), (4) and (5) via a cache. This reduces the CPU load very notably.
      if (mCachedVersion != textSnapshot.Version.VersionNumber) {
        InvalidateCache();
        mCachedVersion = textSnapshot.Version.VersionNumber;
      }
      else if (mCache.TryGetValue(originalSpanToCheck.Span, out var cachedClassifications)) {
        return cachedClassifications;
      }

      var formattedFragments = ParseSpan(originalSpanToCheck);
      var classificationSpans = FormattedFragmentsToClassifications(textSnapshot, formattedFragments);

      mCache[originalSpanToCheck.Span] = classificationSpans;
      return classificationSpans;
    }


    /// <summary>
    /// Parses the given span for comments and the comments themselves for Doxygen commands, markdown, etc.
    /// Returns a list of found fragments that should be formatted. Note that the start index in each returned
    /// fragment is absolute (i.e. relative to the start of the whole text buffer).
    /// </summary>
    public List<FormattedFragment> ParseSpan(SnapshotSpan originalSpanToCheck)
    {      
      // First step: Identify those parts in the span that are actually comments and not code.
      // But do not yet parse the text for the Doxygen commands.
      List<CommentSpan> commentSpans = mSpanSplitter.SplitIntoComments(originalSpanToCheck);

      // Second step: For each identified piece of comment, parse it for Doxygen commands, markdown, etc.
      ITextSnapshot textSnapshot = originalSpanToCheck.Snapshot;
      var result = new List<FormattedFragment>();
      foreach (CommentSpan commentSpan in commentSpans) {
#if !ENABLE_COMMENT_TYPE_DEBUGGING
        if (mGeneralOptions.IsEnabledInCommentType(commentSpan.commentType)) {
          string codeText = textSnapshot.GetText(commentSpan.span);
          var fragmentsToFormat = mParser.Parse(codeText);
          // Convert each fragment start from relative to absolute position.
          result.AddRange(fragmentsToFormat.Select(
            orig => new FormattedFragment(commentSpan.span.Start + orig.StartIndex, orig.Length, orig.Classification)));
        }
#else
        result.Add(new FormattedFragment(commentSpan.span.Start, commentSpan.span.Length, cCommentTypeDebugFormats[commentSpan.commentType]));
#endif
      }

      return result;
    }


    public void Dispose()
    {
      // TODO: This is currently dead code.
      // Visual Studio does not call the Dispose() function. It does get called for ITagger, but not for IClassifier.
      // This looks like a bug: The reasons is probably that the Dispose() method of ClassifierTagger (which owns the
      // IClassifiers) does not call Dispose() on its IClassifiers:
      // https://github.com/microsoft/vs-editor-api/blob/0209a13c58194d4f2a7d03a2615ef03e857547e7/src/Editor/Text/Impl/ClassificationAggregator/ClassifierTagger.cs#L74
      //
      // We could implement our classifier also as ITagger<ClassificationTag>. Then Dispose() works. However, cutting
      // out some text while "rich text editing" is enabled first gets (indirectly) the tagger from the text buffer to
      // format the text in the clipboard, and then ends up calling our Dispose(). Especially, it is called on the instance
      // that is associated with the ordinary text buffer, and which remains valid and is still in use. Thus, from this
      // point forward, our classifier no longer listens to the events that we unsubscribe from here. That looks like
      // another Visual Studio bug.
      //
      // So both ways of doing it is broken due to Visual Studio bugs (VS 17.4.2). Having Dispose() not being called seems
      // like the lesser evil. Especially considering that I failed to trigger a garbage collection that frees the associated
      // ITextBuffer, even when closing the whole Visual Studio solution (but not Visual Studio itself). So Visual Studio
      // appears to not clean up the memory there, too.

      if (mDisposed) {
        return;
      }
      mDisposed = true;

      if (mGeneralOptions != null) {
        mGeneralOptions.SettingsChanged -= OnSettingsOrCommandsChanged;
      }
      if (mParser != null) {
        mParser.ParsingMethodChanged -= OnSettingsOrCommandsChanged;
      }
      if (mVSCppColorer != null) {
        mVSCppColorer.CppColorerReclassifiedSpan -= OnVSCppColorerReclassifiedSpan;
        mVSCppColorer.Dispose();
      }
    }


    public SpanSplitter SpanSplitter { get { return mSpanSplitter; } }


    // When this function is called, the user clicked on "OK" in the options, or the list of Doxygen commands changed.
    // We need to re-initialize most things.
    // Note that with the current implementation, this is fired twice whenever the user clicked "OK" in the options:
    // Once for our own subscription for the options, and once because the 'parser changed' event is also fired
    // every time the user clicked "OK" in the options. In principle, the first call is of course unnecessary. However,
    // we would need to ensure that we are notified after the Doxygen commands got updated. But since this hinges on
    // the same event, getting the order right is fragile. Thus, for now we simply update twice.
    private void OnSettingsOrCommandsChanged(object sender, EventArgs e)
    {
      InvalidateCache();

      // Some of our settings might or might not have changed. Regardless, we force a re-classification of the whole text.
      // This ensures that any possible changes become visible immediately.
      ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(
        new SnapshotSpan(mTextBuffer.CurrentSnapshot, new Span(0, mTextBuffer.CurrentSnapshot.Length))));
    }


    // When this is called, the default Visual Studio cpp colorer updated some colors (i.e. some classifications).
    private void OnVSCppColorerReclassifiedSpan(object sender, SnapshotSpanEventArgs e) 
    {
      InvalidateCache();

      // Since our classification logic is based on the VS cpp colorer (due to the cache, but also because of the SpanSplitter),
      // we need to trigger a reclassification, too. In principle, even without us triggering another reclassification, the one from
      // the VS cpp colorer might be enough. However, this would depend on the execution order of the listeners of the VS cpp
      // colorer's TagsChanged event. This is brittle. So we trigger one ourselves.
      ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(e.Span));
    }


    private void InvalidateCache()
    {
      mCache.Clear();
      mCachedVersion = -1;
      mSpanSplitter.InvalidateCache();
    }


    private List<ClassificationSpan> FormattedFragmentsToClassifications(ITextSnapshot textSnapshot, IEnumerable<FormattedFragment> formattedFragments)
    {
      var result = new List<ClassificationSpan>();
      foreach (FormattedFragment fragment in formattedFragments) {
        IClassificationType classificationInstance = mClassificationEnumToInstance[(uint)fragment.Classification];
        var spanToFormat = new Span(fragment.StartIndex, fragment.Length);
        var snapshotSpan = new SnapshotSpan(textSnapshot, spanToFormat);
        result.Add(new ClassificationSpan(snapshotSpan, classificationInstance));
      }
      return result;
    }


    private readonly ITextBuffer mTextBuffer;
    private readonly IVisualStudioCppColorer mVSCppColorer;
    private readonly SpanSplitter mSpanSplitter;
    private readonly IClassificationType[] mClassificationEnumToInstance;
    private readonly IGeneralOptions mGeneralOptions;
    private readonly CommentParser mParser;

    private Dictionary<Span, IList<ClassificationSpan>> mCache = new Dictionary<Span, IList<ClassificationSpan>>();
    private int mCachedVersion = -1;

    private bool mDisposed = false;

#if ENABLE_COMMENT_TYPE_DEBUGGING
    static readonly Dictionary<CommentType, ClassificationEnum> cCommentTypeDebugFormats = new Dictionary<CommentType, ClassificationEnum> {
      { CommentType.TripleSlash, ClassificationEnum.Command },
      { CommentType.DoubleSlashExclamation, ClassificationEnum.Parameter1 },
      { CommentType.DoubleSlash, ClassificationEnum.Title },
      { CommentType.SlashStarStar, ClassificationEnum.EmphasisMinor },
      { CommentType.SlashStarExclamation, ClassificationEnum.Note },
      { CommentType.SlashStar, ClassificationEnum.InlineCode },
      { CommentType.Unknown, ClassificationEnum.Warning },
    };
#endif
  }

}
