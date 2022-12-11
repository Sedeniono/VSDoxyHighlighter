using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace VSDoxyHighlighter
{
  //=======================================================================================
  // Utilities
  //=======================================================================================

  internal enum CommentType
  {
    TripleSlash, // "///"
    DoubleSlashExclamation, // "//!"
    DoubleSlash, // "//", followed by anything except "/" or "!"
    SlashStarStar, // "/**"
    SlashStarExclamation, // "/*!"
    SlashStar, // "/*", followed by anything except "*" or "!"
    Unknown // Something went wrong
  }


  /// <summary>
  /// A span representing a comment, together with associated type of the comment.
  /// </summary>
  internal struct CommentSpan
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
  /// Represents a fragment of a comment as identified by the Visual Studio default tagger.
  /// </summary>
  struct VSCommentFragment
  {
    public VSCommentFragment(IEnumerable<ITagSpan<IClassificationTag>> currentTags_, int currentIndexOfComment_)
    {
      currentTags = currentTags_;
      tagIndex = currentIndexOfComment_;
    }

    // The character index (0 means start of file) of the comment's start.
    public int fragmentStartCharIdx { get { return fragmentTag.Span.Start; } }

    public ITagSpan<IClassificationTag> fragmentTag { get { return currentTags.ElementAt(tagIndex); } }

    public string GetTextTrimmed()
    {
      return fragmentTag.Span.GetText().TrimEnd(cNewlineChars);
    }


    // The tags associated with the whole line, as classified by the Visual Studio default tagger.
    private IEnumerable<ITagSpan<IClassificationTag>> currentTags;

    // The index into "currentTags" that contains the considered comment.
    private int tagIndex;

    private static readonly char[] cNewlineChars = new char[] { '\n', '\r' };
  }


  //=======================================================================================
  // SpanSplitter
  //=======================================================================================

  /// <summary>
  /// Class responsible for diving up some spans into comments, and identify the type of the 
  /// comment ("//", "///", "/*", etc). It is NOT responsible for any doxygen formatting.
  /// </summary>
  internal class SpanSplitter
  {
    public SpanSplitter(ITextBuffer textBuffer) 
    {
      mTextBuffer = textBuffer;
      mCommentTypeIdentification = new CommentTypeIdentification();
    }

    /// <summary>
    /// Decomposes the given span into comments. The returned list of spans all represent comments.
    /// I.e. it filters out all text in the given span that is NOT within a comment, and returns the remaining
    /// parts as list.
    /// 
    /// Every comment receives its own entry. For example, if the input is "/**//**/", the returned list
    /// contains two entries.
    /// </summary>
    public List<CommentSpan> SplitIntoComments(SnapshotSpan spanToCheck)
    {
      // The task is to somehow figure out which parts of the given text represents a comment and which does not.
      // This is something that is absolutely non-trivial in C++:
      //  - Just looking at single lines is by far not sufficient because of multiline comments (/* ... */) and also
      //    because of line continuation (i.e. a "\" at the end of e.g. a normal C++ "//"-style comment causes the
      //    comment to continue in the next line).
      //  - Even a single line might contain multiple independent comments: "/*comment*/code/*comment*/code"
      //    contains two comments.
      //  - In case of C-style multiline comments, simply scanning upwards to the next "/*" to find whether some token
      //    is in a comment is insufficient because of strings and "//" style comments. Detecting strings by itself is
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
      //     those spans that were classified as comments. This is the idea from https://stackoverflow.com/q/19060596/3740047
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
      //
      // ==> We not only need to know whether some token is in a comment, but also how that comment was started. Doxygen
      //     does not read "//" and "/*" comments, only "///", "/**", etc. Unfortunately, the VS tagger does not provide
      //     us with that information directly. Thus, the logic becomes again a bit more complicated, see IdentifyCommentType().

      var vsCppTagger = FindDefaultVSCppTagger();
      if (vsCppTagger != null) {
        try {
          IEnumerable<ITagSpan<IClassificationTag>> vsCppTags 
            = vsCppTagger.GetTags(new NormalizedSnapshotSpanCollection(spanToCheck.Snapshot, spanToCheck.Span));

          //System.Diagnostics.Debug.WriteLine("For: " + spanToCheck.GetText().Replace("\r\n", "\\n"));
          var result = new List<CommentSpan>();
          int tagCount = vsCppTags.Count();
          for (int tagIndex = 0; tagIndex < tagCount; ++tagIndex) {
            var vsTag = vsCppTags.ElementAt(tagIndex);
            if (!spanToCheck.Span.OverlapsWith(vsTag.Span)) {
              continue;
            }

            if (IsVSComment(vsTag)) {
              CommentType type = mCommentTypeIdentification.IdentifyCommentType(
                spanToCheck.Snapshot, new VSCommentFragment(vsCppTags, tagIndex), vsCppTagger);
              result.Add(new CommentSpan(vsTag.Span, type));
              //System.Diagnostics.Debug.WriteLine("\t'" + vsTag.Span.GetText().Replace("\r\n", "\\n") + "' ==> " + vsTag.Tag.ClassificationType.Classification + " ==> " + type.ToString());
            }
            //else {
            //  System.Diagnostics.Debug.WriteLine("\t'" + vsTag.Span.GetText().Replace("\r\n", "\\n") + "' ==> " + vsTag.Tag.ClassificationType.Classification);
            //}
          }

          return result;
        }
        catch (System.NullReferenceException ex) {
          // The "vsCppTagger" throws a NullReferenceException if one renames a file that has not a C++ ending (.cpp, .cc, etc.)
          // (and thus has initially no syntax highlighting) to a name with a C++ ending (e.g. .cpp). I think the tagger that
          // we find for the very first classification is the wrong one, or something like that. The problem vanishes when we
          // search the default tagger again when a re-classification gets triggered a bit later.
          ActivityLog.LogWarning(
            "VSDoxyHighlighter",
            $"Probably the 'vsCppTagger' threw a NullReferenceException. Exception message: {ex.ToString()}");
          mDefaultVSCppTagger = null;
          return new List<CommentSpan>() { new CommentSpan(spanToCheck.Span, CommentType.Unknown) };
        }
      }
      else {
        // Mh, no tagger found? Maybe Microsoft changed their tagger name?
        ActivityLog.LogWarning("VSDoxyHighlighter", "The 'vsCppTagger' is null.");
        return new List<CommentSpan>() { new CommentSpan(spanToCheck.Span, CommentType.Unknown) };
      }
    }


    /// <summary>
    /// Returns true if the given tag corresponds to one of the tags used by Visual Studio for comments.
    /// </summary>
    static public bool IsVSComment(ITagSpan<IClassificationTag> vsTag)
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


    /// <summary>
    /// Retrives the tagger of Visual Studio that is responsible for classifying C++ code. 
    /// See comment in DecomposeSpanIntoComments().
    /// </summary>
    private ITagger<IClassificationTag> FindDefaultVSCppTagger()
    {
      if (mDefaultVSCppTagger == null) {
        string nameOfDefaultCppTagger = "Microsoft.VisualC.CppColorer".ToUpper();
        foreach (var kvp in mTextBuffer.Properties.PropertyList) {
          if (kvp.Value is ITagger<IClassificationTag> casted) {
            if (kvp.Key.ToString().ToUpper() == nameOfDefaultCppTagger) {
              mDefaultVSCppTagger = casted;
              break;
            }
          }
        }
      }

      return mDefaultVSCppTagger;
    }


    private readonly ITextBuffer mTextBuffer;
    private readonly CommentTypeIdentification mCommentTypeIdentification;

    // Cached tagger that is used by Visual Studio to classify C/C++ code.
    private ITagger<IClassificationTag> mDefaultVSCppTagger = null;

  }



  //=======================================================================================
  // CommentTypeIdentification
  //=======================================================================================

  /// <summary>
  /// Class responsible to figure out the comment type of a given comment fragment.
  /// </summary>
  class CommentTypeIdentification
  {
    /// <summary>
    /// Given a specific fragment of a comment (as identified by the Visual Studio's default tagger),
    /// returns the type of that comment ("//", "///", "/*", etc).
    /// </summary>
    public CommentType IdentifyCommentType(
      ITextSnapshot textSnapshot, 
      VSCommentFragment inputFragment,
      ITagger<IClassificationTag> defaultVSTagger)
    {
      // The task is to find the start of the comment which contains the given comment fragment "inputFragment".
      // Looking at the found start, we can check its type.
      // The basic idea is to go to the next character BEFORE the start of the "inputFragment", and ask the
      // VS tagger whether we are still in a comment or not. If we are not, we know that the fragment also
      // starts the comment. Otherwise, we need to go further back until we hit a character that is not within
      // a comment.
      // The VS tagger does decompose multiple comments on a line for us (e.g. "/**//**/" gives two tags).
      // Unfortunately, it does not do this for comments on successive lines with only the newline charcater
      // in-between. Thus, the logic becomes more complicated: After having found a non-comment token, we need
      // to go forward again line by line and check ourselves whether a comment fragment ends the comment.

      if (textSnapshot.Version.VersionNumber != mFileVersionOfCache) {
        // File was edited, reset cache.
        mCommentTypeCache.Clear();
        mFileVersionOfCache = textSnapshot.Version.VersionNumber;
      }

      // Stack: We go BACKWARD through the lines. So the top element of the stack represents
      // a line earlier in the text file.
      Stack<VSCommentFragment> fragmentsInReverseOrder = null;

      // Loop backward through the lines in the text buffer, until we hit the start of a comment (where
      // we are 100% sure that it is a start), or we hit the file start, or we hit a line where we
      // cached the comment type.
      VSCommentFragment curFragment = inputFragment;
      CommentType cachedTypeOfFragmentWhereStopped = CommentType.Unknown;
      while (true) {
        (bool foundStart, VSCommentFragment? lastFragmentInPreviousLine) 
          = FragmentContainsCommentStartAssuredly(curFragment.fragmentTag, defaultVSTagger);

        // Performance optimization: If we could immediately identify the start of the comment to be at the start
        // of the original input fragment, no need to do all the more complicated logic and allocations. Return directly.
        if (fragmentsInReverseOrder == null) {
          if (foundStart) {
            return IdentifyTypeOfCommentStartingAt(textSnapshot, curFragment.fragmentStartCharIdx);
          }
          fragmentsInReverseOrder = new Stack<VSCommentFragment>();
        }

        fragmentsInReverseOrder.Push(curFragment);
        if (foundStart) {
          break;
        }

        // If we hit a line where we already know the comment type from a previous call, we can stop.
        CommentType cachedType;
        if (mCommentTypeCache.TryGetValue(curFragment.fragmentStartCharIdx, out cachedType)) {
          cachedTypeOfFragmentWhereStopped = cachedType;
          break;
        }

        curFragment = lastFragmentInPreviousLine.Value;
      }

      // Now loop forward again through the lines that we considered, and check whether we actually missed
      // some comment ends.
      curFragment = fragmentsInReverseOrder.Pop();
      int curCommentStartCharIdx = curFragment.fragmentStartCharIdx;
      CommentType curCommentType =
        (cachedTypeOfFragmentWhereStopped == CommentType.Unknown)
          ? IdentifyTypeOfCommentStartingAt(textSnapshot, curCommentStartCharIdx)
          : cachedTypeOfFragmentWhereStopped;
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


    /// <summary>
    /// Assumes that the given comment fragment in "fragment" is of the comment type "type". 
    /// Then returns true if the comment fragment ends, and false if the comment continues in the next line.
    /// </summary>
    private bool IsCommentFragmentTerminated(CommentType type, VSCommentFragment fragment)
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
    private (bool foundStart, VSCommentFragment? lastFragmentInPreviousLine)
      FragmentContainsCommentStartAssuredly(ITagSpan<IClassificationTag> commentFragment, ITagger<IClassificationTag> defaultVSTagger)
    {
      Debug.Assert(SpanSplitter.IsVSComment(commentFragment));

      var textSnapshot = commentFragment.Span.Snapshot;

      // Backtrack: Find previous character that is not a newline.
      (int charIdxBeforeComment, int numSkippedNewlines) = FindNonNewlineCharacterIndexBefore(textSnapshot, commentFragment.Span.Start);
      if (charIdxBeforeComment < 0) {
        // Reached the start of the document without finding a non-newline character
        // --> The input comment is "complete", i.e. the comment starts at its beginning.
        return (true, null);
      }

      // charIdxBeforeComment now points to a non-newline character before the start of the comment.
      // Classify it. Note that the vsCppTagger seems to always return the classifications for the whole line, even if
      // given just a single character. But to be on the safe side, and because we need to classification of the whole
      // line further down, we give it the whole line right here.
      ITextSnapshotLine previousLine = textSnapshot.GetLineFromPosition(charIdxBeforeComment);
      var tagsOfPreviousLine = defaultVSTagger.GetTags(new NormalizedSnapshotSpanCollection(textSnapshot, previousLine.Extent.Span));
      if (tagsOfPreviousLine.Count() <= 0) {
        // The default tagger e.g. does not return any tags if a line contains only whitespace that is *outside* of a comment.
        return (true, null);
      }

      int indexOfPreviousCommentTag = TryFindIndexOfCommentTagContainingCharIndex(tagsOfPreviousLine, charIdxBeforeComment);
      bool charIdxBeforeInputCommentIsInAComment = indexOfPreviousCommentTag >= 0;
      if (!charIdxBeforeInputCommentIsInAComment) {
        // The relevant character before the input comment is not a comment
        // --> The input comment is "complete", i.e. the comment starts at its beginning.
        return (true, null);
      }

      if (charIdxBeforeComment >= 1 && textSnapshot.GetText(charIdxBeforeComment - 1, 2) == "*/") {
        // For example: "/*foo1*//*foo2*/"
        // The default tagger already produces separate tags for the two C-style comments. Assume we started
        // with "/*foo2*/", and charIdxBeforeComment now points to the second "/" in the string (which closes the first comment).
        // So if [charIdxBeforeComment-1,charIdxBeforeComment] == "*/", then the input comment is "complete".
        return (true, null);
      }

      // The only way how we came that far, should have been if we backtracked to a previous line.
      // If we didn't, something is fishy. Give up to prevent infinite recursion.
      if (numSkippedNewlines == 0) {
        Debug.Assert(false);
        return (true, null);
      }

      return (false, new VSCommentFragment(tagsOfPreviousLine, indexOfPreviousCommentTag));
    }


    /// <summary>
    /// Given the tags from the Visual Studio tagger, returns the index of that tag which contains the given 
    /// character index and which represents a comment. Returns -1 if charIdxBeforeComment does not point to a comment.
    /// A value of charIdx=0 means the start of the text document.
    /// </summary>
    private int TryFindIndexOfCommentTagContainingCharIndex(IEnumerable<ITagSpan<IClassificationTag>> vsTags, int charIdx)
    {
      // We loop backward since comments tend to be at the end of lines after code (if there is any code).
      // 
      // There might be several tags associated with the charIdxBeforeComment. For example, for XML comments, there is one tag for the whole
      // comment and then individual parts for the special highlights of e.g. XML parameters. IsVSComment() ignores all those
      // special highlights.

      int indexOfCommentTagInLine = vsTags.Count() - 1;
      while (indexOfCommentTagInLine >= 0) {
        var curElem = vsTags.ElementAt(indexOfCommentTagInLine);
        if (curElem.Span.Contains(charIdx) && SpanSplitter.IsVSComment(curElem)) {
          return indexOfCommentTagInLine;
        }
        --indexOfCommentTagInLine;
      }

      return -1;
    }


    /// <summary>
    /// Starting at startIndex-1, goes backward through the text until a non-newline character is found. 
    /// Returns the index of that character and the number of lines that the function went back. 
    /// Returns -1 as character index if there is no such character.
    /// </summary>
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


    // For every start index of some comment fragment (index in terms of the character index in the file), we cache
    // the resulting comment type for performance reasons. The cache gets reset after every edit.
    private int mFileVersionOfCache = -1;
    private Dictionary<int /*commentFragmentStartCharIdx*/, CommentType> mCommentTypeCache = new Dictionary<int, CommentType>();
  }

}
