using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;



namespace VSDoxyHighlighter
{
  public class FragmentsMatcherMarkdownEmphasisAndStrikethrough : IFragmentsMatcher
  {
    private class EmphasisSpan
    {
      public string EmphasisMarker;

      // The start marker is [StartEmphasisStartIdx..StartEmphasisEndIdx).
      public int StartEmphasisStartIdx;
      public int StartEmphasisEndIdx;

      // The end marker is [EndEmphasisStartIdx..EndEmphasisEndIdx).
      public int EndEmphasisStartIdx;
      public int EndEmphasisEndIdx;

      public bool IsValid = true;

      // Null if not yet set.
      public EmphasisCategory? Category;
    }


    private struct EmphasisCategory
    {
      public bool IsStrikethrough;
      public int EmphasisLevel;
    }


    private class EmphasisSpanWithChildren
    {
      public EmphasisSpan Span;
      public List<EmphasisSpanWithChildren> Children;
    }


    public IList<FormattedFragmentGroup> FindFragments(string text)
    {
      int searchStartIdx = 0;
      List<FormattedFragmentGroup> fragments = null;

      while (searchStartIdx < text.Length) {
        (int nextSearchStartIdx, var foundFragments) = FindNextEmphasisBlocksAsFragments(text, searchStartIdx, text.Length);
        if (nextSearchStartIdx < 0) {
          break;
        }

        if (foundFragments != null && foundFragments.Count > 0) {
          fragments = fragments ?? new List<FormattedFragmentGroup>();
          fragments.AddRange(foundFragments);
        }

        Debug.Assert(nextSearchStartIdx > searchStartIdx);
        searchStartIdx = nextSearchStartIdx;
      }

      return fragments ?? cEmptyList;
    }


    private static (int nextSearchStartIdx, List<FormattedFragmentGroup> fragments) FindNextEmphasisBlocksAsFragments(
        string text, int searchStartIdx, int searchEndIdx)
    {
      EmphasisSpanWithChildren span = FindNextEmphasisBlock(text, searchStartIdx, searchEndIdx);
      if (span == null) {
        return (-1, null);
      }

      FindAndMarkInvalidEmphasisSpans(span);
      CategorizeEmphasisSpans(span);
      RemoveInnerSpansNotChangingEmphasis(span);
      MergeNestedWithoutGaps(span);

      List<FormattedFragment> flattenedFragments = FlattenNestedEmphasisSpans(span);
      var fragmentsInGroups = flattenedFragments.Select(
        f => new FormattedFragmentGroup(new List<FormattedFragment>() { f })).ToList();
      int nextSearchStartIdx = flattenedFragments.Count > 0
        ? flattenedFragments.Last().EndIndex
        : span.Span.EndEmphasisEndIdx;
      return (nextSearchStartIdx, fragmentsInGroups);
    }


    private static EmphasisSpanWithChildren FindNextEmphasisBlock(
        string text, int searchStartIdx, int searchEndIdx)
    {
      int nextInnerSearchStartIdx = -1;
      EmphasisSpan nextOuterSpan = null;
      while (searchStartIdx < searchEndIdx) {
        (nextInnerSearchStartIdx, nextOuterSpan) = FindEmphasis(text, searchStartIdx, searchEndIdx);
        if (nextInnerSearchStartIdx < 0) {
          Debug.Assert(nextOuterSpan == null);
          return null;
        }
        Debug.Assert(nextInnerSearchStartIdx > searchStartIdx);
        Debug.Assert(nextInnerSearchStartIdx <= searchEndIdx);
        if (nextOuterSpan == null) {
          searchStartIdx = nextInnerSearchStartIdx;
          continue;
        }
        break;
      }

      if (nextOuterSpan == null) {
        return null;
      }

      var spanWithChildren = new EmphasisSpanWithChildren() {
        Span = nextOuterSpan,
        Children = new List<EmphasisSpanWithChildren>()
      };

      while (true) {
        EmphasisSpanWithChildren child =
          FindNextEmphasisBlock(text, nextInnerSearchStartIdx, nextOuterSpan.EndEmphasisStartIdx);
        if (child == null) {
          break;
        }

        Debug.Assert(child.Span.EndEmphasisEndIdx <= nextOuterSpan.EndEmphasisStartIdx);
        Debug.Assert(child.Span.StartEmphasisStartIdx >= nextOuterSpan.StartEmphasisEndIdx);
        spanWithChildren.Children.Add(child);
        nextInnerSearchStartIdx = child.Span.EndEmphasisEndIdx;
      }

      return spanWithChildren;
    }


    private static (int nextSearchStartIdx, EmphasisSpan foundSpan) FindEmphasis(
        string text, int searchStartIdx, int searchEndIdx)
    {
      (int startEmphasisStartIdx, int startEmphasisEndIdx) = FindEmphasisStart(text, searchStartIdx, searchEndIdx);
      if (startEmphasisStartIdx < 0) {
        return (-1, null); // Give up the search completely, no more emphasis fragments can be found.
      }
      if (startEmphasisEndIdx < 0) {
        // Some emphasis starts at `searchStartIdx`, but it cannot be completed before reaching `searchEndIdx`.
        // Try again by starting with the next character. For example, some imbalanced emphasis like `**test*`
        // can produce this.
        return (startEmphasisStartIdx + 1, null);
      }
      Debug.Assert(startEmphasisStartIdx < startEmphasisEndIdx);
      Debug.Assert(startEmphasisEndIdx <= searchEndIdx);

      char emphasisChar = text[startEmphasisStartIdx];
      int emphasisMarkerLen = startEmphasisEndIdx - startEmphasisStartIdx;
      string emphasisMarker = new string(emphasisChar, emphasisMarkerLen);
      (int endEmphasisStartIdx, int endEmphasisEndIdx) = FindEmphasisEnd(text, startEmphasisEndIdx, searchEndIdx, emphasisMarker);
      if (endEmphasisEndIdx < 0) {
        // Continue search after the start, not after the end, since there might be valid emphasis in-between.
        return (startEmphasisStartIdx + 1, null);
      }
      Debug.Assert(endEmphasisStartIdx >= 0);
      Debug.Assert(endEmphasisStartIdx < endEmphasisEndIdx);
      Debug.Assert(endEmphasisEndIdx <= searchEndIdx);

      if (text.IndexOf('\n', startEmphasisEndIdx, endEmphasisStartIdx - startEmphasisEndIdx) >= 0) {
        // Emphasis cannot span multiple lines.
        // Continue search after the start, not after the end, since there might be valid emphasis in-between.
        return (startEmphasisStartIdx + 1, null);
      }

      var foundSpan = new EmphasisSpan() {
        EmphasisMarker = emphasisMarker,
        StartEmphasisStartIdx = startEmphasisStartIdx,
        StartEmphasisEndIdx = startEmphasisEndIdx,
        EndEmphasisStartIdx = endEmphasisStartIdx,
        EndEmphasisEndIdx = endEmphasisEndIdx
      };

      return (startEmphasisEndIdx, foundSpan);
    }


    private static (int startEmphasisStartIdx, int startEmphasisEndIdx) FindEmphasisStart(
        string text, int searchStartIdx, int searchEndIdx)
    {
      Debug.Assert(searchStartIdx >= 0);
      Debug.Assert(searchEndIdx <= text.Length);
      if (searchEndIdx - searchStartIdx < 2) {
        return (-1, -1); // Not enough space for start and end of emphasis
      }

      int startEmphasisStartIdx = text.IndexOfAny(new char[] { '*', '_', '~' }, searchStartIdx);
      if (startEmphasisStartIdx < 0
        // If there is not at least one character for the closing of the emphasis remaining, we can give up immediately.  
        || startEmphasisStartIdx >= searchEndIdx - 1) {
        return (-1, -1);
      }

      if (!IsBeforeEmphasisStartAllowed(text, startEmphasisStartIdx)) {
        searchStartIdx = startEmphasisStartIdx + 1;
        return (startEmphasisStartIdx, -1);
      }

      char emphasisChar = text[startEmphasisStartIdx];
      int startEmphasisEndIdx = startEmphasisStartIdx + 1;
      while (startEmphasisEndIdx < searchEndIdx
             && text[startEmphasisEndIdx] == emphasisChar) {
        ++startEmphasisEndIdx;
      }

      if (!IsAfterEmphasisStartAllowed(text, startEmphasisEndIdx)) {
        searchStartIdx = startEmphasisStartIdx + 1;
        return (startEmphasisStartIdx, -1);
      }

      return (startEmphasisStartIdx, startEmphasisEndIdx);
    }


    private static bool IsBeforeEmphasisStartAllowed(string text, int startEmphasisStartIdx)
    {
      Debug.Assert(startEmphasisStartIdx >= 0);
      if (startEmphasisStartIdx == 0) {
        return true;
      }
      char p = text[startEmphasisStartIdx - 1];
      return
        // Only specific characters are allowed before the emphasisMarker start: https://www.doxygen.nl/manual/markdown.html#mddox_emph_spans
        char.IsWhiteSpace(p)
        || p == '<' || p == '{' || p == '(' || p == '[' || p == ',' || p == ':' || p == ';'
        // These are not documented but are allowed according to the Doxygen source code.
        || p == '\'' || p == '>'
        // Needed to allow nesting, e.g. *__text__*
        || p == '*' || p == '_' || p == '~';
    }


    private static bool IsAfterEmphasisStartAllowed(string text, int startEmphasisEndIdx)
    {
      if (startEmphasisEndIdx >= text.Length) {
        return false;
      }

      char a = text[startEmphasisEndIdx];
      return !char.IsWhiteSpace(a);
    }


    private static bool IsAlphanumericAsInDoxygen(char c)
    {
      // Conditions copied from Doxygen source code (Markdown::Private::processEmphasis()).
      if (('a' <= c && c <= 'z') || ('A' <= c && c <= 'Z') || ('0' <= c && c <= '9')) {
        return true;
      }
      if (c >= 0x80) {
        return true; // Some unicode character
      }
      return false;
    }


    private static (int endEmphasisStartIdx, int endEmphasisEndIdx) FindEmphasisEnd(
        string text, int searchStartIdx, int searchEndIdx, string emphasisMarker)
    {
      while (searchStartIdx < searchEndIdx) {
        Debug.Assert(searchStartIdx >= 0);
        int endEmphasisStartIdx = text.IndexOf(emphasisMarker, searchStartIdx, StringComparison.Ordinal);
        if (endEmphasisStartIdx < 0 || endEmphasisStartIdx >= searchEndIdx) {
          return (-1, -1);
        }

        // If the emphasisMarker is e.g. "*" (a single "*") and then we encounter a "**",
        // we want to skip this completely. This is simply the way Doxygen behaves.
        if (endEmphasisStartIdx + 1 + emphasisMarker.Length <= searchEndIdx
            && text.Substring(endEmphasisStartIdx + 1, emphasisMarker.Length) == emphasisMarker) {
          searchStartIdx = endEmphasisStartIdx;
          do {
            ++searchStartIdx;
          }
          while (searchStartIdx + emphasisMarker.Length <= searchEndIdx
                 && text.Substring(searchStartIdx, emphasisMarker.Length) == emphasisMarker);

          continue;
        }

        if (!IsBeforeEmphasisEndAllowed(text, endEmphasisStartIdx)) {
          searchStartIdx = endEmphasisStartIdx + 1;
          continue;
        }
        int endEmphasisEndIdx = endEmphasisStartIdx + emphasisMarker.Length;
        if (endEmphasisEndIdx > searchEndIdx) {
          return (-1, -1);
        }
        if (!IsAfterEmphasisEndAllowed(text, endEmphasisEndIdx)) {
          searchStartIdx = endEmphasisStartIdx + 1;
          continue;
        }
        return (endEmphasisStartIdx, endEmphasisEndIdx);
      }

      return (-1, -1);
    }


    private static bool IsBeforeEmphasisEndAllowed(string text, int endEmphasisStartIdx)
    {
      Debug.Assert(endEmphasisStartIdx >= 0);
      if (endEmphasisStartIdx == 0) {
        return false;
      }
      char p = text[endEmphasisStartIdx - 1];
      // https://www.doxygen.nl/manual/markdown.html#mddox_emph_spans
      bool isNotAllowed
        = char.IsWhiteSpace(p)
        || p == '(' || p == '{' || p == '[' || p == '<' || p == '=' || p == '+' || p == '-' || p == '\\' || p == '@';
      return !isNotAllowed;
    }


    private static bool IsAfterEmphasisEndAllowed(string text, int endEmphasisEndIdx)
    {
      if (endEmphasisEndIdx >= text.Length) {
        return true;
      }
      char a = text[endEmphasisEndIdx];
      // https://www.doxygen.nl/manual/markdown.html#mddox_emph_spans
      return !IsAlphanumericAsInDoxygen(a);
    }


    private static void FindAndMarkInvalidEmphasisSpans(EmphasisSpanWithChildren span)
    {
      Debug.Assert(span != null);

      // Doxygen's behavior is peculiar: If the first marker is "*", "**", "_" or "__",
      // and it is immediately followed by "~~", then both markers are not recognized.
      // For example "*~~" does not start an italic strikethrough.
      // But: "***~~" does actuall start a bold-italic strikethrough.
      // This code here reproduces Doxygen's behavior.
      if (span.Children.Count > 0) {
        var firstChild = span.Children[0];
        if (firstChild.Span.StartEmphasisStartIdx == span.Span.StartEmphasisEndIdx
            && firstChild.Span.EmphasisMarker == "~~") {
          string m = span.Span.EmphasisMarker;
          switch (span.Span.EmphasisMarker) {
            case "*":
            case "_":
            case "**":
            case "__":
              span.Span.IsValid = false;
              firstChild.Span.IsValid = false;
              break;
          }
        }
      }

      foreach (var child in span.Children) {
        FindAndMarkInvalidEmphasisSpans(child);
      }
    }


    private static void CategorizeEmphasisSpans(
        EmphasisSpanWithChildren span, int nestingLevel = 0, int emphasisLevel = 0, bool strikethrough = false)
    {
      Debug.Assert(span != null);

      if (span.Span.IsValid) {
        switch (span.Span.EmphasisMarker) {
          case "~~":
            strikethrough = true;
            break;

          case "*":
          case "_":
            if (emphasisLevel == 0 || emphasisLevel == 2) {
              ++emphasisLevel;
            }
            break;

          case "**":
          case "__":
            if (emphasisLevel == 0 || emphasisLevel == 1) {
              emphasisLevel += 2;
            }
            break;

          case "***":
          case "___":
            emphasisLevel = Math.Max(emphasisLevel, 3);
            break;

          default:
            break; // Skip this span, it contains an unsupported marker.
        }
      }

      span.Span.Category = new EmphasisCategory() {
        IsStrikethrough = strikethrough,
        EmphasisLevel = emphasisLevel
      };

      foreach (var child in span.Children) {
        CategorizeEmphasisSpans(child, nestingLevel + 1, emphasisLevel, strikethrough);
      }
    }


    private static void RemoveInnerSpansNotChangingEmphasis(EmphasisSpanWithChildren span)
    {
      Debug.Assert(span != null);
      if (span.Span.Category == null) {
        return;
      }
      var referenceCategory = span.Span.Category.Value;

      for (int childIdx = 0; childIdx < span.Children.Count; ++childIdx) { 
        var child = span.Children[childIdx];
        if (child.Span.Category == null 
            || (child.Span.Category.Value.EmphasisLevel == referenceCategory.EmphasisLevel
                && child.Span.Category.Value.IsStrikethrough == referenceCategory.IsStrikethrough)) {
          // "Dissolve" this child, as it does not change the emphasis.
          span.Children.RemoveAt(childIdx);
          span.Children.InsertRange(childIdx, child.Children);
          --childIdx;
          continue;
        }
        RemoveInnerSpansNotChangingEmphasis(child);
      }
    }


    private static void MergeNestedWithoutGaps(EmphasisSpanWithChildren span)
    {
      Debug.Assert(span != null);
      if (span.Span.Category == null || span.Children.Count != 1) {
        return;
      }

      var child = span.Children[0];
      Debug.Assert(child.Span.Category != null);
      if (child.Span.StartEmphasisStartIdx == span.Span.StartEmphasisEndIdx
          && child.Span.EndEmphasisEndIdx == span.Span.EndEmphasisStartIdx) {
        var origSpan = span.Span;
        var mergedSpan = child.Span;
        mergedSpan.StartEmphasisStartIdx = origSpan.StartEmphasisStartIdx;
        mergedSpan.EndEmphasisEndIdx = origSpan.EndEmphasisEndIdx;
        mergedSpan.EmphasisMarker = origSpan.EmphasisMarker + mergedSpan.EmphasisMarker;
        span.Span = mergedSpan;
        span.Children = child.Children;
        // We keep mergedSpan.Category as it is because we already "accumulated" the
        // emphasis levels in CategorizeEmphasisSpans().

        MergeNestedWithoutGaps(span);
      }
    }


    private static List<FormattedFragment> FlattenNestedEmphasisSpans(EmphasisSpanWithChildren span)
    {
      Debug.Assert(span != null);

      var flattenedSpans = new List<FormattedFragment>();

      ClassificationEnum? outerSpanClassification = GetClassificationFor(span.Span.Category);
      if (outerSpanClassification == null) {
        foreach (var child in span.Children) {
          var childFlattenedSpans = FlattenNestedEmphasisSpans(child);
          if (childFlattenedSpans == null || childFlattenedSpans.Count == 0) {
            continue;
          }
          flattenedSpans.AddRange(childFlattenedSpans);
        }
      }
      else {
        int startIdx = span.Span.StartEmphasisStartIdx;

        foreach (var child in span.Children) {
          var childFlattenedSpans = FlattenNestedEmphasisSpans(child);
          if (childFlattenedSpans == null || childFlattenedSpans.Count == 0) {
            continue;
          }
          var firstChildFragment = childFlattenedSpans.First();
          flattenedSpans.Add(new FormattedFragment(
              startIndex: startIdx,
              length: firstChildFragment.StartIndex - startIdx,
              classification: outerSpanClassification.Value));
          flattenedSpans.AddRange(childFlattenedSpans);
          var lastChildFragment = childFlattenedSpans.Last();
          startIdx = lastChildFragment.StartIndex + lastChildFragment.Length;
        }

        flattenedSpans.Add(new FormattedFragment(
            startIndex: startIdx,
            length: span.Span.EndEmphasisEndIdx - startIdx,
            classification: outerSpanClassification.Value));
      }

#if DEBUG
      // Verify that the fragments are successive.
      for (int i = 0; i < flattenedSpans.Count - 1; ++i) {
        FormattedFragment currentFragment = flattenedSpans[i];
        FormattedFragment nextFragment = flattenedSpans[i + 1];
        Debug.Assert(currentFragment.Length > 0);
        Debug.Assert(nextFragment.Length > 0);
        Debug.Assert(currentFragment.EndIndex + 1 == nextFragment.StartIndex);
      }
      if (flattenedSpans.Count > 0) {
        Debug.Assert(flattenedSpans[0].Length > 0);
      }
#endif

      return flattenedSpans;
    }


    private static ClassificationEnum? GetClassificationFor(EmphasisCategory? category)
    {
      if (category == null) {
        return null;
      }

      bool strike = category.Value.IsStrikethrough;

      switch (category.Value.EmphasisLevel) {
        case 0:
          return strike ? ClassificationEnum.Strikethrough : (ClassificationEnum?)null;
        case 1:
          return strike ? ClassificationEnum.StrikethroughEmphasisMinor : ClassificationEnum.EmphasisMinor;
        case 2:
          return strike ? ClassificationEnum.StrikethroughEmphasisMajor : ClassificationEnum.EmphasisMajor;
        case 3:
          return strike ? ClassificationEnum.StrikethroughEmphasisHuge : ClassificationEnum.EmphasisHuge;
        default:
          // More than level 3 is not supported by Doxygen.
          return null;
      }
    }


    private static readonly List<FormattedFragmentGroup> cEmptyList = new List<FormattedFragmentGroup>();
  }
}
