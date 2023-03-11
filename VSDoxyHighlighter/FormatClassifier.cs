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

namespace VSDoxyHighlighter
{
  // Enumeration of all possible classifications. We could have also used the string IDs, but an enum is more convenient
  // (e.g. to find all occurrences).
  // Order doesn't matter, but the numeric values are used as indices!
  public enum ClassificationEnum : uint
  {
    Command1,
    Note,
    Warning,
    Exceptions,
    Parameter1,
    Parameter2,
    Title,
    EmphasisMinor,
    EmphasisMajor,
    Strikethrough,
    InlineCode
  }


  /// <summary>
  /// Identifiers for the classifications. E.g., Visual Studio will use these strings as keys
  /// to store the classification's configuration in the registry.
  /// </summary>
  public static class IDs
  {
    public const string ID_command1 = "VSDoxyHighlighter_Command";
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

    public static readonly Dictionary<ClassificationEnum, string> ToID = new Dictionary<ClassificationEnum, string>(){
        {ClassificationEnum.Command1, ID_command1},
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
      };
  }






  /// <summary>
  /// Tells Visual Studio via MEF about the classifications provided by the extension.
  /// </summary>
  internal static class CommentClassificationDefinitions
  {
#pragma warning disable 169
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_command1)]
    private static ClassificationTypeDefinition typeDefinitionForCommand;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_warningKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForWarningKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_noteKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForNoteKeyword;
    
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_exceptions)]
    private static ClassificationTypeDefinition typeDefinitionForExceptions;

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

      mVSCppColorer = new DefaultVSCppColorerImpl(textBuffer);
      mVSCppColorer.CppColorerReclassifiedSpan += OnVSCppColorerReclassifiedSpan;

      mSpanSplitter = new SpanSplitter(mVSCppColorer);
      // CommentCommandCompletionSource needs it to check whether some point is inside of a comment.
      textBuffer.Properties.AddProperty(typeof(SpanSplitter), mSpanSplitter);

      int numClassifications = Enum.GetNames(typeof(ClassificationEnum)).Length;
      Debug.Assert(numClassifications == IDs.ToID.Count);
      mClassificationEnumToInstance = new IClassificationType[numClassifications];
      foreach (var enumAndID in IDs.ToID) {
        IClassificationType classificationType = registry.GetClassificationType(enumAndID.Value);
        Debug.Assert(classificationType != null);
        mClassificationEnumToInstance[(uint)enumAndID.Key] = classificationType;
      }

      ThreadHelper.ThrowIfNotOnUIThread();
      mGeneralOptions = VSDoxyHighlighterPackage.GeneralOptions;
      mGeneralOptions.SettingsChanged += OnSettingsChanged;

      InitCommentFormatter();
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

      // First step: Identify the comment sections in the given span.
      List<CommentSpan> commentSpans = mSpanSplitter.SplitIntoComments(originalSpanToCheck);
      
      // Second step: Apply the formatting to each comment span.
      var result = new List<ClassificationSpan>();
      foreach (CommentSpan commentSpan in commentSpans) {
#if !ENABLE_COMMENT_TYPE_DEBUGGING
        if (mGeneralOptions.IsEnabledInCommentType(commentSpan.commentType)) {
          string codeText = textSnapshot.GetText(commentSpan.span);

          // Scan the given text for keywords and get the proper formatting for it.
          var fragmentsToFormat = mFormatter.FormatText(codeText);

          // Convert the list of fragments that should be formatted to Visual Studio types.
          foreach (FormattedFragment fragment in fragmentsToFormat) {
            IClassificationType classificationInstance = mClassificationEnumToInstance[(uint)fragment.Classification];
            var spanToFormat = new Span(commentSpan.span.Start + fragment.StartIndex, fragment.Length);
            var snapshotSpan = new SnapshotSpan(textSnapshot, spanToFormat);
            result.Add(new ClassificationSpan(snapshotSpan, classificationInstance));
          }
        }
#else
        IClassificationType classificationType = mClassificationEnumToInstance[(uint)cCommentTypeDebugFormats[commentSpan.commentType]];
        result.Add(new ClassificationSpan(new SnapshotSpan(textSnapshot, commentSpan.span), classificationType));
#endif
      }

      mCache[originalSpanToCheck.Span] = result;
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
        mGeneralOptions.SettingsChanged -= OnSettingsChanged;
      }
      if (mVSCppColorer != null) {
        mVSCppColorer.CppColorerReclassifiedSpan -= OnVSCppColorerReclassifiedSpan;
        mVSCppColorer.Dispose();
      }
      
      mTextBuffer.Properties.RemoveProperty(typeof(SpanSplitter));
    }



    // When this function is called, the user clicked on "OK" in the options.
    private void OnSettingsChanged(object sender, EventArgs e)
    {
      InitCommentFormatter();
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


    private void InitCommentFormatter()
    {
      var commandGroups = DoxygenCommands.ApplyConfigList(mGeneralOptions.DoxygenCommandsConfig);
      mFormatter = new CommentFormatter(commandGroups);
    }


    private readonly ITextBuffer mTextBuffer;
    private readonly IVisualStudioCppColorer mVSCppColorer;
    private readonly SpanSplitter mSpanSplitter;
    private readonly IClassificationType[] mClassificationEnumToInstance;
    private readonly GeneralOptionsPage mGeneralOptions;

    private CommentFormatter mFormatter;

    private Dictionary<Span, IList<ClassificationSpan>> mCache = new Dictionary<Span, IList<ClassificationSpan>>();
    private int mCachedVersion = -1;

    private bool mDisposed = false;

#if ENABLE_COMMENT_TYPE_DEBUGGING
    static readonly Dictionary<CommentType, ClassificationEnum> cCommentTypeDebugFormats = new Dictionary<CommentType, ClassificationEnum> {
      { CommentType.TripleSlash, ClassificationEnum.Command1 },
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
