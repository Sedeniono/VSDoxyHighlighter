// If this is enabled, we disable the doxygen highlighting and instead highlight
// the various comment types ("//", "///", "/*", etc.). This allows easier debugging
// of the logic to detect the comment types.
#define ENABLE_COMMENT_TYPE_DEBUGGING

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Shell;
using System.Linq;
using Microsoft.VisualStudio.Language.CodeCleanUp;

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
  internal class CommentClassifier : IClassifier
  {
    private struct CommentSpan
    {
      public Span span;
      public CommentType commentType;

      public CommentSpan(Span span_, CommentType commentType_)
      {
        span = span_;
        commentType = commentType_;
      }
    }

    private enum CommentType
    {
      TripleSlash, // "///"
      DoubleSlashExclamation, // "//!"
      DoubleSlash, // "//", followed by anything except "/" or "!"
      SlashStarStar, // "/**"
      SlashStarExclamation, // "/*!"
      SlashStar, // "/*", followed by anything except "*" or "!"
      Unknown // Something went wrong
    }


    internal CommentClassifier(IClassificationTypeRegistryService registry, ITextBuffer textBuffer)
    {
      mTextBuffer = textBuffer;
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
    }

#pragma warning disable 67
    // TODO: We need to call this with a certain span S if we decide that span S should be re-classified.
    // E.g. might need to call this from listening to TextEditor-Changed events, and somewhere the opening of
    // a comment is inserted, and thus all following lines need to be re-classified.
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67


    int counter = 0;

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

      counter++;
      System.Diagnostics.Debug.WriteLine($"Version={textSnapshot.Version.VersionNumber}, Line={textSnapshot.GetLineNumberFromPosition(originalSpanToCheck.Span.Start)}, counter={counter}");

      List<CommentSpan> commentSpans = DecomposeSpanIntoComments(originalSpanToCheck);
      
      var result = new List<ClassificationSpan>();
      foreach (CommentSpan commentSpan in commentSpans) {
#if !ENABLE_COMMENT_TYPE_DEBUGGING
        Debug.Assert(commentSpan.commentType != CommentType.Unknown);
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

      return result;
    }


    private bool ApplyHighlightingToCommentType(CommentType type) 
    {
      switch (type) {
        case CommentType.SlashStarStar:
        case CommentType.SlashStarExclamation:
        case CommentType.TripleSlash:
        case CommentType.DoubleSlashExclamation:
          return true;
        default:
          return false;
      }
    }


    /// <summary>
    /// Decomposes the given snapshot spans such that only a list of spans is returned that are all comments.
    /// I.e. it filters out all text in the given span that is NOT within a comment, and returns the remaining
    /// parts as list.
    /// </summary>
    private List<CommentSpan> DecomposeSpanIntoComments(SnapshotSpan spanToCheck)
    {
      // The task is to somehow figure out which parts of the given text represents a comment and which does not.
      // This is something that is absolutely non-trivial in C++:
      //  - Just looking at single lines is by far not sufficient because of multiline comments (/* ... */) and also
      //    because of line continuation (i.e. a "\" at the end of e.g. a normal C++ "//"-style comment causes the
      //    comment to continue in the next line).
      //  - Even a single line might contain multiple independent comments: /* comment */ code /* comment */ code
      //  - In case of multiline comments, simply scanning upwards to the next "/*" to find whether some toke is in
      //    a comment is insufficient because of strings and "//" style comment. Detecting strings by itself is
      //    also highly non-trivial due to similar reasons (multiline strings, raw strings, line continuation via "\").
      //  - A "/*" might not start a comment if there is another "/*" before without a corresponding "*/".
      //    I.e. in "/* foo1 /* foo2 */" the "/*" after "foo1" does not start the comment.
      //  - The code should be fast. Due to the global character of the multiline comments, some sort of caching needs
      //    to be applied. The cache needs to be updated whenever some text changes, but the update should be as local
      //    as possible for performance reasons. Yet, if the user types e.g. "/*", potentially everything afterwards
      //    needs to be re-classified as comment (or not).
      //
      // ==> We do not attempt to implement this. Especially considering that Visual Studio itself must somewhere
      //     somehow already have solved this task. The "somewhere" is in the tagger named "Microsoft.VisualC.CppColorer".
      //     Therefore, we get a reference to that tagger, and ask it to decompose the given span for us. We only keep
      //     those spans that were classified as tokens. This is the idea from https://stackoverflow.com/q/19060596/3740047
      //     Note that all of this is a hack since the VS tagger is not really exposed via a proper API. 
      //     An alternative might be to use the IClassifierAggregatorService (https://stackoverflow.com/a/29311144/3740047).
      //     I have not really tried it, but I fear that calling GetClassifier() calls our code, and thus we might end up with
      //     an infinite recursion. Of course, there would be ways to bypass this problem (if it really does occur). But
      //     the approach with the dedicated tagger seems favorable since it limits the request to only that specific tagger,
      //     and does not involve all the other existing classifiers.
      //
      // ==> There are basically two disadvantages with this approach: First, we cannot really write automated tests for it
      //     because we would need to have a running Visual Studio instance. Second, it is a hack and thus might break without
      //     warning in a future Visual Studio. But the alternative to re-implement the classification logic seems even more
      //     ridiculous.

      var vsCppTagger = FindDefaultVSCppTagger();
      if (vsCppTagger != null) {
        IEnumerable<ITagSpan<IClassificationTag>> vsCppTags = null;
        try {
          vsCppTags = vsCppTagger.GetTags(new NormalizedSnapshotSpanCollection(spanToCheck.Snapshot, spanToCheck.Span));
        }
        catch (System.NullReferenceException ex) {
          // The "vsCppTagger" throws a NullReferenceException if one renames a file that has not a C++ ending (.cpp, .cc, etc.)
          // (and thus has initially no syntax highlighting) to a name with a C++ ending (e.g. .cpp). I guess the "vsCppTagger"
          // is not yet initialized completely. The problem vanishes after re-opening the file.
          // Simply return the whole span to format; it might lead to some false positives, but as far as I know not too many.
          ActivityLog.LogWarning(
            "VSDoxyHighlighter",
            $"The 'vsCppTagger' threw a NullReferenceException. Exception message: {ex.ToString()}");
          return new List<CommentSpan>() { new CommentSpan(spanToCheck.Span, CommentType.Unknown) };
        }

        //System.Diagnostics.Debug.WriteLine("For: " + spanToCheck.GetText().Replace("\r\n", "\\n"));
        var result = new List<CommentSpan>();
        int tagCount = vsCppTags.Count();
        for (int tagIndex = 0; tagIndex < tagCount; ++tagIndex) {
          var vsTag = vsCppTags.ElementAt(tagIndex);
          if (!spanToCheck.Span.OverlapsWith(vsTag.Span)) {
            continue;
          }

          if (IsVSComment(vsTag)) {
            CommentType type = IdentifyCommentType(spanToCheck.Snapshot, vsCppTags, tagIndex);
            result.Add(new CommentSpan(vsTag.Span, type));
            //System.Diagnostics.Debug.WriteLine("\t'" + vsTag.Span.GetText().Replace("\r\n", "\\n") + "' ==> " + vsTag.Tag.ClassificationType.Classification + " ==> " + type.ToString());
          }
          //else {
          //  System.Diagnostics.Debug.WriteLine("\t'" + vsTag.Span.GetText().Replace("\r\n", "\\n") + "' ==> " + vsTag.Tag.ClassificationType.Classification);
          //}
        }

        return result;
      }
      else {
        // Mh, no tagger found? Maybe Microsoft changed their tagger name?
        // Simply return the whole span to format; it might lead to some false positives, but as far as I know not too many.
        ActivityLog.LogWarning("VSDoxyHighlighter", "The 'vsCppTagger' is null.");
        return new List<CommentSpan>() { new CommentSpan(spanToCheck.Span, CommentType.Unknown) };
      }
    }


    /// <summary>
    /// Returns true if the given vsTag corresponds to one of the tags used by Visual Studio for comments.
    /// </summary>
    private bool IsVSComment(ITagSpan<IClassificationTag> vsTag)
    {
      string classification = vsTag.Tag.ClassificationType.Classification;
      // Visual Studio currently knows two different comment types: "comment" and "XML Doc Comment".
      // Note that the strings are independent of the language configured in Visual Studio.
      if (classification.ToUpper() == "COMMENT" || classification.ToUpper() == "XML DOC COMMENT") {
        return true;
      }
      else { 
        return false;
      }
    }



    private CommentType IdentifyTypeOfCommentStartingAt(ITextSnapshot textSnapshot, int startOfCommentCharIndex) 
    {
      string commentStart = textSnapshot.GetText(
        startOfCommentCharIndex, Math.Min(4, textSnapshot.Length - startOfCommentCharIndex));
      if (commentStart.StartsWith("///")) {
        return CommentType.TripleSlash;
      }
      else if (commentStart.StartsWith("//!")) {
        return CommentType.DoubleSlashExclamation;
      }
      else if (commentStart.StartsWith("//")) {
        return CommentType.DoubleSlash;
      }
      else if (commentStart == "/**/") {
        // Special case of an "empty" comment.
        return CommentType.SlashStar;
      }
      else if (commentStart.StartsWith("/**")) {
        return CommentType.SlashStarStar;
      }
      else if (commentStart.StartsWith("/*!")) {
        return CommentType.SlashStarExclamation;
      }
      else if (commentStart.StartsWith("/*")) {
        return CommentType.SlashStar;
      }
      else {
        return CommentType.Unknown;
      }
    }


    private bool IsCppCommentType(CommentType type) 
    {
      switch (type) {
        case CommentType.TripleSlash:
        case CommentType.DoubleSlashExclamation:
        case CommentType.DoubleSlash:
          return true;

        default:
          return false;
      }
    }


    private bool IsCCommentType(CommentType type)
    {
      switch (type) {
        case CommentType.SlashStarStar:
        case CommentType.SlashStarExclamation:
        case CommentType.SlashStar:
          return true;

        default:
          return false;
      }
    }


    private struct CommentFragmentInLine 
    {
      // The character index (0 means start of file) of the comment's start.
      public int fragmentStartCharIdx { get { return currentTags.ElementAt(tagIndex).Span.Start; } }

      public CommentFragmentInLine(IEnumerable<ITagSpan<IClassificationTag>> currentTags_, int currentIndexOfComment_)
      {
        currentTags = currentTags_;
        tagIndex = currentIndexOfComment_;
      }

      public string GetTextTrimmed()
      {
        return currentTags.ElementAt(tagIndex).Span.GetText().TrimEnd(cNewlineChars);
      }

      // The tags associated with the whole line, as classified by the Visual Studio default tagger.
      private IEnumerable<ITagSpan<IClassificationTag>> currentTags;

      // The index into "currentTags" that contains the considered comment.
      private int tagIndex;

      private static readonly char[] cNewlineChars = new char[] { '\n', '\r' };
    }



    /// <summary>
    /// Given a specific fragment of a comment (as identified by the Visual Studio's default tagger)
    /// in `lineTags.ElementAt(indexOfCommentFragment)`, returns the type of that comment ("//", "///", "/*", etc).
    /// 
    /// Note: The remaining elements in "lineTags" need to be the tags for the remaining part
    /// of the line; these are expected since the caller of this function actually nows them already,
    /// so getting them again is unecessary.
    /// </summary>
    private CommentType IdentifyCommentType(
        ITextSnapshot textSnapshot,
        IEnumerable<ITagSpan<IClassificationTag>> lineTags,
        int indexOfCommentFragment)
    {
      if (textSnapshot.Version.VersionNumber != mFileVersionOfCache) {
        // File was edited, reset cache.
        mCommentTypeCache.Clear();
        mFileVersionOfCache = textSnapshot.Version.VersionNumber;
      }

      // From first to last elements, we go BACKWARD in the text buffer.
      Stack<CommentFragmentInLine> fragmentsInReverseOrder = new Stack<CommentFragmentInLine>();
      CommentType foundCacheElement = CommentType.Unknown;

      // Loop backward through the lines in the text buffer, until we hit the start of a comment (where
      // we are 100% sure that it is a start), or we hit the file start, or we hit a line where we
      // cached the comment type.
      IEnumerable<ITagSpan<IClassificationTag>> curLineTags = lineTags;
      int curIndexOfCommentFragment = indexOfCommentFragment;
      while (true) {
        (bool foundStart, var previousLineTags, int indexOfLastCommentIndexInPreviousLine) 
          = FragmentContainsCommentStartAssuredly(curLineTags.ElementAt(curIndexOfCommentFragment));
        var newFragment = new CommentFragmentInLine(curLineTags, curIndexOfCommentFragment);
        fragmentsInReverseOrder.Push(newFragment);
        if (foundStart) {
          // TODO: Optimize for the case that we have not looped. Can return immediately.
          break;
        }

        // If we hit a line where we already know the comment type from a previous call, we can stop.
        CommentType cachedElement;
        if (mCommentTypeCache.TryGetValue(newFragment.fragmentStartCharIdx, out cachedElement)) {
          foundCacheElement = cachedElement;
          break;
        }

        curLineTags = previousLineTags;
        curIndexOfCommentFragment = indexOfLastCommentIndexInPreviousLine;
      }

      // Now loop forward again through the lines that we considered, and check whether we actually missed the
      // end of comment. Figuring this out depends on the top-most comment style.
      CommentFragmentInLine curFragment = fragmentsInReverseOrder.Pop();
      int curCommentStartCharIdx = curFragment.fragmentStartCharIdx;
      CommentType curCommentType =
        (foundCacheElement == CommentType.Unknown) 
        ? IdentifyTypeOfCommentStartingAt(textSnapshot, curCommentStartCharIdx) 
        : foundCacheElement;
      Debug.Assert(curCommentType != CommentType.Unknown);

      while (fragmentsInReverseOrder.Count() > 0) {
        Debug.Assert(curCommentType != CommentType.Unknown);
        mCommentTypeCache[curFragment.fragmentStartCharIdx] = curCommentType;

        bool curCommentTerminates = IsCommentFragmentTerminated(curCommentType, curFragment);
        curFragment = fragmentsInReverseOrder.Pop(); // Go to the next line

        if (curCommentTerminates) {
          curCommentStartCharIdx = curFragment.fragmentStartCharIdx;
          curCommentType = IdentifyTypeOfCommentStartingAt(textSnapshot, curCommentStartCharIdx);
        }
      }

      Debug.Assert(curCommentType != CommentType.Unknown);
      mCommentTypeCache[curFragment.fragmentStartCharIdx] = curCommentType;

      Debug.Assert(curCommentStartCharIdx >= 0);
      return curCommentType;
    }


    // Assumes that the given comment fragment in "fragment" is of the comment type "type".
    // Then returns true if the comment fragment ends, and false if the comment continues in the next line.
    private bool IsCommentFragmentTerminated(CommentType type, CommentFragmentInLine fragment) 
    {
      // For example:
      //     /**/
      //     // foo
      // Assume that the inputCommentTag is the "//". Because there is only an newline between the two lines,
      // the backtracked to "/*". Assume we are in the first iteration, i.e. curFragment is the "/*". Here we need to detect that
      // the "/*" gets terminated by "*/". Therefore, we need to adapt the comment start for "//".
      // On the other hand, consider
      //     /*
      //     // foo
      //     */
      // If the inputCommentTag is the "//" again, then we do want to return "/*" as comment start. I.e. the line with "//"
      // is actually a "/*" comment.
      if (IsCCommentType(type) && fragment.GetTextTrimmed().EndsWith("*/")) {
        return true;
      }
      // For example:
      //     // fooX
      //     /**/
      // Assume that the inputCommentTag is the "/**/" and that we backtracked to "//". In this case we need to recognized
      // that the "//" comment ends, so that we eventually return "/**/".
      // However, assume the following: 
      //     // fooX \
      //     /**/
      // The backslash forces a line continuation. In this case the "/**/" is actually a "//" comment.
      else if (IsCppCommentType(type) && !fragment.GetTextTrimmed().EndsWith("\\")) {
        return true;
      }

      return false;
    }


    /// <summary>
    /// Returns true if the start of the comment fragment is also the start of the whole comment, and this is 100% assured.
    /// Returns false if the comment might or might not start at the fragment start (i.e. more work needs to be done
    /// to identify this).
    /// 
    /// If false is returned, the function already needed to retrieve the tags from the Visual Studio's default tagger 
    /// for a previous line. For performance reasons, the function returns the result in this case.
    /// </summary>
    private (bool foundStart, IEnumerable<ITagSpan<IClassificationTag>> previousLineTags, int indexOfLastCommentIndexInPreviousLine) 
      FragmentContainsCommentStartAssuredly(ITagSpan<IClassificationTag> commentFragment)
    {
      Debug.Assert(IsVSComment(commentFragment));

      var textSnapshot = commentFragment.Span.Snapshot;

      // Backtrack: Find previous character that is not a newline.
      (int charIdxBeforeComment, int numSkippedNewlines) = FindNonNewlineCharacterIndexBefore(textSnapshot, commentFragment.Span.Start);
      if (charIdxBeforeComment < 0) {
        // Reached the start of the document without finding a non-newline character
        // --> The input comment is "complete", i.e. the comment starts at its beginning.
        return (true, null, -1);
      }

      // charIdxBeforeComment now points to a non-newline character before the start of the comment.
      // Classify it. Note that the vsCppTagger seems to always return the classifications for the whole line, even if
      // given just a single character. But to be on the safe side, and because we need to classification of the whole
      // line further down, we give it the whole line right here.
      var defaultTagger = FindDefaultVSCppTagger();
      ITextSnapshotLine previousLine = textSnapshot.GetLineFromPosition(charIdxBeforeComment);
      var tagsOfPreviousLine = defaultTagger.GetTags(new NormalizedSnapshotSpanCollection(textSnapshot, previousLine.Extent.Span));
      if (tagsOfPreviousLine.Count() <= 0) {
        // The default tagger e.g. does not return any tags if a line contains only whitespace that is *outside* of a comment.
        return (true, null, -1);
      }

      int indexOfPreviousCommentTag = TryFindIndexOfCommentTagContainingIndex(tagsOfPreviousLine, charIdxBeforeComment);
      bool charIdxBeforeInputCommentIsInAComment = indexOfPreviousCommentTag >= 0;
      if (!charIdxBeforeInputCommentIsInAComment) {
        // The relevant character before the input comment is not a comment
        // --> The input comment is "complete", i.e. the comment starts at its beginning.
        return (true, null, -1);
      }

      if (charIdxBeforeComment >= 1 && textSnapshot.GetText(charIdxBeforeComment - 1, 2) == "*/") {
        // For example: "/*foo1*//*foo2*/"
        // The default tagger already produces separate tags for the two C-style comments. Assume we started
        // with "/*foo2*/", and charIdxBeforeComment now points to the second "/" in the string (which closes the first comment).
        // So if [charIdxBeforeComment-1,charIdxBeforeComment] == "*/", then the input comment is "complete".
        return (true, null, -1);
      }

      // The only way how we came that far, should have been if we backtracked to a previous line.
      // If we didn't, something is fishy. Give up to prevent infinite recursion.
      if (numSkippedNewlines == 0) {
        Debug.Assert(false);
        return (true, null, -1);
      }

      return (false, tagsOfPreviousLine, indexOfPreviousCommentTag);
    }


    // Given the tags from the Visual Studio tagger, returns the index of that tag which contains the given
    // character index and which represents a comment. Returns -1 if charIdxBeforeComment does not point to a comment.
    // A value of charIdxBeforeComment=0 means the start of the text document.
    private int TryFindIndexOfCommentTagContainingIndex(IEnumerable<ITagSpan<IClassificationTag>> tags, int charIdx)
    {
      // We loop backward since comments tend to be at the end of lines after code (if there is any code).
      // 
      // There might be several tags associated with the charIdxBeforeComment. For example, for XML comments, there is one tag for the whole
      // comment and then individual parts for the special highlights of e.g. XML parameters. IsVSComment() ignores all those
      // special highlights.

      int indexOfCommentTagInLine = tags.Count() - 1;
      while (indexOfCommentTagInLine >= 0) {
        var curElem = tags.ElementAt(indexOfCommentTagInLine);
        if (curElem.Span.Contains(charIdx) && IsVSComment(curElem)) {
          return indexOfCommentTagInLine;
        }
        --indexOfCommentTagInLine;
      }

      return -1;
    }


    // Starting at startIndex-1, goes backward through the text until a non-newline character is found.
    // Returns the index of that character. Returns -1 if there is none.
    private (int charIdx, int numSkippedNewlines) FindNonNewlineCharacterIndexBefore(ITextSnapshot textSnapshot, int startIndex)
    {
      int charIdx = startIndex - 1;
      int numSkippedNewlines = 0;
      while (charIdx >= 0) {
        string curChar = textSnapshot.GetText(charIdx, 1);
        if (curChar != "\n" && curChar != "\r") {
          break;
        }
        if (curChar == "\n") {
          ++numSkippedNewlines;
        }
        --charIdx;
      }

      return (charIdx, numSkippedNewlines);
    }


    // Retrives the tagger of Visual Studio that is responsible for classifying C++ code.
    // See comment in DecomposeSpanIntoComments().
    private ITagger<IClassificationTag> FindDefaultVSCppTagger()
    {
      if (mDefaultCppTagger == null) {
        string nameOfDefaultCppTager = "Microsoft.VisualC.CppColorer".ToUpper();
        foreach (var kvp in mTextBuffer.Properties.PropertyList) {
          if (kvp.Value is ITagger<IClassificationTag> casted) {
            if (kvp.Key.ToString().ToUpper() == nameOfDefaultCppTager) {
              mDefaultCppTagger = casted;
              break;
            }
          }
        }
      }

      return mDefaultCppTagger;
    }


    private readonly ITextBuffer mTextBuffer;
    private readonly CommentFormatter mFormater;
    private readonly IClassificationType[] mFormatTypeToClassificationType;

    private ITagger<IClassificationTag> mDefaultCppTagger = null;

    // For every start index of some comment fragment (index in terms of the character index in the file), we cache
    // the resulting comment type for performance reasons. The cache gets reset after every edit.
    private int mFileVersionOfCache = -1;
    private Dictionary<int /*commentFragmentStartCharIdx*/, CommentType> mCommentTypeCache = new Dictionary<int, CommentType>();


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
