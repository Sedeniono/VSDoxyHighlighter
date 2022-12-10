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

      List<CommentSpan> commentSpans = DecomposeSpanIntoComments(originalSpanToCheck);

      var result = new List<ClassificationSpan>();
      foreach (CommentSpan commentSpan in commentSpans) {
        if (!originalSpanToCheck.Span.OverlapsWith(commentSpan.span)) {
          continue;
        }

#if true
        string codeText = textSnapshot.GetText(commentSpan.span);

        // Scan the given text for keywords and get the proper formatting for it.
        var fragmentsToFormat = mFormater.FormatText(codeText);

        // Convert the list of fragments that should be formatted to Visual Studio types.
        foreach (FormattedFragment fragment in fragmentsToFormat) {
          IClassificationType classificationType = mFormatTypeToClassificationType[(uint)fragment.Type];
          var spanToFormat = new Span(commentSpan.span.Start + fragment.StartIndex, fragment.Length);
          result.Add(new ClassificationSpan(new SnapshotSpan(textSnapshot, spanToFormat), classificationType));
        }
#else
        IClassificationType classificationType = mFormatTypeToClassificationType[(uint)cCommentTypeDebugFormats[commentSpan.commentType]];
        result.Add(new ClassificationSpan(new SnapshotSpan(textSnapshot, commentSpan.span), classificationType));
#endif
      }

      return result;
    }


    static readonly Dictionary<CommentType, FormatType> cCommentTypeDebugFormats = new Dictionary<CommentType, FormatType> {
      { CommentType.TripleSlash, FormatType.Command },
      { CommentType.DoubleSlashExclamation, FormatType.Parameter1 },
      { CommentType.DoubleSlash, FormatType.Title },
      { CommentType.SlashStarStar, FormatType.EmphasisMinor },
      { CommentType.SlashStarExclamation, FormatType.Note },
      { CommentType.SlashStar, FormatType.InlineCode },
      { CommentType.Unknown, FormatType.Warning },
    };

    struct CommentSpan
    {
      public Span span;
      public CommentType commentType;

      public CommentSpan(Span span_, CommentType commentType_) 
      {
        span = span_;
        commentType = commentType_;
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

      var defaultTagger = FindDefaultVSCppTagger();
      if (defaultTagger != null) {
        IEnumerable<ITagSpan<IClassificationTag>> defaultTags = null;
        try {
          defaultTags = defaultTagger.GetTags(new NormalizedSnapshotSpanCollection(spanToCheck.Snapshot, spanToCheck.Span));
        }
        catch (System.NullReferenceException ex) {
          // The "defaultTagger" throws a NullReferenceException if one renames a file that has not a C++ ending (.cpp, .cc, etc.)
          // and thus has initially no syntax highlighting to a name with a C++ ending (e.g. .cpp). I guess the "defaultTagger"
          // is not yet initialized completely. The problem vanishes after re-opening the file.
          // Simply return the whole span to format; it might lead to some false positives, but as far as I know not too many.
          ActivityLog.LogWarning(
            "VSDoxyHighlighter",
            $"The 'defaultTagger' threw a NullReferenceException. Formatting everything. Exception message: {ex.ToString()}");
          return new List<CommentSpan>() { new CommentSpan(spanToCheck.Span, CommentType.Unknown) };
        }

        //System.Diagnostics.Debug.WriteLine("For: " + spanToCheck.GetText().Replace("\r\n", "\\n"));
        var result = new List<CommentSpan>();
        int tagCount = defaultTags.Count();
        for (int tagIndex = 0; tagIndex < tagCount; ++tagIndex) {
          var tag = defaultTags.ElementAt(tagIndex);
          if (IsComment(tag)) {
            CommentType type = IdentifyCommentType(spanToCheck.Snapshot, defaultTagger, defaultTags, tagIndex);
            result.Add(new CommentSpan(tag.Span, type));
            //System.Diagnostics.Debug.WriteLine(
            //  "\t'" + tag.Span.GetText().Replace("\r\n", "\\n") 
            //  + "' ==> " + tag.Tag.ClassificationType.Classification + " ==> " + type.ToString());
          }
          //else {
          //  System.Diagnostics.Debug.WriteLine("\t'" + tag.Span.GetText().Replace("\r\n", "\\n") + "' ==> " + tag.Tag.ClassificationType.Classification);
          //}
        }

        return result;
      }
      else {
        // Mh, no tagger found? Maybe Microsoft changed their tagger name?
        // Simply return the whole span to format; it might lead to some false positives, but as far as I know not too many.
        ActivityLog.LogWarning("VSDoxyHighlighter", "The 'defaultTagger' is null. Formatting everything.");
        return new List<CommentSpan>() { new CommentSpan(spanToCheck.Span, CommentType.Unknown) };
      }
    }


    private bool IsComment(ITagSpan<IClassificationTag> tag)
    {
      string classification = tag.Tag.ClassificationType.Classification;
      // Visual Studio currently knows two different comment types: "comment" and "XML Doc Comment".
      // Note that the strings are independent of the language configured in Visual Studio.
      if (classification.ToUpper() == "COMMENT" || classification.ToUpper() == "XML DOC COMMENT") {
        return true;
      }
      else { 
        return false;
      }
    }

    enum CommentType
    {
      TripleSlash, // "///"
      DoubleSlashExclamation, // "//!"
      DoubleSlash, // "//", followed by anything except "/" or "!"
      SlashStarStar, // "/**"
      SlashStarExclamation, // "/*!"
      SlashStar, // "/*", followed by anything except "*" or "!"
      Unknown // Something went wrong
    }

    private CommentType IdentifyCommentType(
        ITextSnapshot textSnapshot,
        ITagger<IClassificationTag> defaultTagger,
        IEnumerable<ITagSpan<IClassificationTag>> tags,
        int indexOfCommentTagToIdentify)
    {
      int commentStartIndexInSnapshot = FindCommentStart(defaultTagger, tags, indexOfCommentTagToIdentify);
      return IdentifyTypeOfCommentStartingAt(textSnapshot, commentStartIndexInSnapshot);
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
        Debug.Assert(false);
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


    // Given the tags from the Visual Studio tagger, returns the index of that tag which covers the given
    // character index and which represents a comment. Returns -1 if charIdxBeforeComment does not point to a comment.
    // A value of charIdxBeforeComment=0 means the start of the text document.
    private int TryFindIndexOfCommentTagContainingIndex(IEnumerable<ITagSpan<IClassificationTag>> tags, int charIdx) 
    {
      // We loop backward since comments tend to be at the end of lines after code (if there is any code).
      // 
      // There might be several tags associated with the charIdxBeforeComment. For example, for XML comments, there is one tag for the whole
      // comment and then individual parts for the special highlights of e.g. XML parameters. IsComment() ignores all those
      // special highlights.

      int indexOfCommentTagInLine = tags.Count() - 1;
      while (indexOfCommentTagInLine >= 0) {
        var curElem = tags.ElementAt(indexOfCommentTagInLine);
        if (curElem.Span.Contains(charIdx) && IsComment(curElem)) {
          return indexOfCommentTagInLine;
        }
        --indexOfCommentTagInLine;
      }

      return -1;
    }

    // Starting at startIndex-1, goes backward through the text until a non-newline character is found.
    // Returns the index of that character. Returns -1 if there is none.
    private (int charIdx, int numSkippedNewlines) FindRelevantCharacterIndexBefore(ITextSnapshot textSnapshot, int startIndex)
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

    // Given the tags "inputTags" of some line and the index to a comment tag in this "inputTags",
    // returns the character index where the comment actually starts in the text snapshot.
    // A return value of 0 means the beginning of the file.
    private int FindCommentStart(
        ITagger<IClassificationTag> defaultTagger,
        IEnumerable<ITagSpan<IClassificationTag>> inputTags,
        int indexOfComment)
    {
      var inputCommentTag = inputTags.ElementAt(indexOfComment);
      Debug.Assert(IsComment(inputCommentTag));

      var textSnapshot = inputCommentTag.Span.Snapshot;

      // Backtrack: Find previous character that is not a newline.
      (int charIdxBeforeComment, int numSkippedNewlines) = FindRelevantCharacterIndexBefore(textSnapshot, inputCommentTag.Span.Start);
      if (charIdxBeforeComment < 0) {
        // Reached the start of the document without finding a non-newline character
        // --> The input comment is "complete", i.e. the comment starts at its beginning.
        return inputCommentTag.Span.Start;
      }

      // charIdxBeforeComment now points to a non-newline character before the start of the comment.
      // Classify it. Note that the defaultTagger seems to always return the classifications for the whole line, even if
      // given just a single character. But to be on the safe side, and because we need to classification of the whole
      // line further down, we give it the whole line right here.
      ITextSnapshotLine wholeLine = textSnapshot.GetLineFromPosition(charIdxBeforeComment);
      var lineTags = defaultTagger.GetTags(new NormalizedSnapshotSpanCollection(textSnapshot, wholeLine.Extent.Span));
      if (lineTags.Count() <= 0) {
        // The default tagger e.g. does not return any tags if a line contains only whitespace that is outside of a comment.
        return inputCommentTag.Span.Start;
      }

      int indexOfPreviousCommentTag = TryFindIndexOfCommentTagContainingIndex(lineTags, charIdxBeforeComment);
      if (indexOfPreviousCommentTag < 0) {
        // The relevant character before the input comment is not a comment
        // --> The input comment is "complete", i.e. the comment starts at its beginning.
        return inputCommentTag.Span.Start;
      }

      if (charIdxBeforeComment >= 1 && textSnapshot.GetText(charIdxBeforeComment - 1, 2) == "*/") {
        // For example: "/*foo1*//*foo2*/"
        // The default tagger already produces separate tags for the two C-style comments. Assume we started
        // with "/*foo2*/", and charIdxBeforeComment now points to the second "/" in the string (which closes the first comment).
        // So if [charIdxBeforeComment-1,charIdxBeforeComment] == "*/", then the input comment is "complete".
        return inputCommentTag.Span.Start;
      }

      // The only way how we came that far, should have been if we backtracked to a previous line.
      // If we didn't, something is fishy. Give up to prevent infinite recursion.
      if (numSkippedNewlines == 0) {
        Debug.Assert(false);
        return inputCommentTag.Span.Start;
      }


      // We did not detect the comment for sure so far. There are only newline characters between the inputCommentTag and the previous comment.
      // Thus, since numSkippedNewlines>0 at this point here, we are looking at the previous lines now.
      int earlierCommentStartCharIdx = FindCommentStart(defaultTagger, lineTags, indexOfPreviousCommentTag);
      CommentType earlierCommentStartType = IdentifyTypeOfCommentStartingAt(textSnapshot, earlierCommentStartCharIdx);
      string earlierCommentText = lineTags.ElementAt(indexOfPreviousCommentTag).Span.GetText().TrimEnd(new char[] { '\n', '\r' });
      
      // For example:
      //     /**/
      //     // foo
      // Assume that the inputCommentTag is the "//". Then the above FindCommentStart() returns the "/*".
      // However, the "/*" gets terminated by "*/" in the previous line, too. Therefore, we need to return the start of "//".
      // On the other hand, consider
      //     /*
      //     // foo
      //     */
      // If the inputCommentTag is the "//" again, then we do want to return "/*" as returned by FindCommentStart().
      if (IsCCommentType(earlierCommentStartType)) {
        if (earlierCommentText.EndsWith("*/")) {
          return inputCommentTag.Span.Start;
        }
      }
      // For example:
      //     // fooX
      //     /**/
      // Assume that the inputCommentTag is the "/**/". Then the above FindCommentStart() returns the "//".
      // In this case we do NOT want to return the result found by FindCommentStart(), since the "//" comment ends
      // at the end of the previous line. Instead, we want to return the start of "/**/".
      // However, assume the following: 
      //     // fooX \
      //     /**/
      // The backslash forces a line continuation. In this case we do want to return the start of "//" as the comment start.
      else if (IsCppCommentType(earlierCommentStartType)) {
        if (!earlierCommentText.EndsWith("\\")) {
          return inputCommentTag.Span.Start;
        }
      }
        
      return earlierCommentStartCharIdx;
    }


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
  }

}
