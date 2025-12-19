using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;



namespace VSDoxyHighlighter
{
  public class FragmentsMatcherMarkdownEmphasisAndStrikethrough : IFragmentsMatcher
  {
    private struct EmphasisSpan
    {
      public string EmphasisMarker;

      // The start marker is [StartEmphasisStartIdx..StartEmphasisEndIdx).
      public int StartEmphasisStartIdx;
      public int StartEmphasisEndIdx;

      // The end marker is [EndEmphasisStartIdx..EndEmphasisEndIdx).
      public int EndEmphasisStartIdx;
      public int EndEmphasisEndIdx;
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


    private struct CategorizedEmphasisSpan
    {
      public EmphasisSpan Span;
      public bool IsStrikethrough;
      public int EmphasisLevel;
    }


    private static (int nextSearchStartIdx, List<FormattedFragmentGroup> fragments) FindNextEmphasisBlocksAsFragments(
        string text, int searchStartIdx, int searchEndIdx)
    {
      (int nextSearchStartIdx, List<EmphasisSpan> nestedSpans) = FindNextEmphasisBlock(text, searchStartIdx, searchEndIdx);
      if (nextSearchStartIdx < 0) {
        Debug.Assert(nestedSpans == null);
        return (-1, null);
      }

      List<CategorizedEmphasisSpan> categorizedNestedSpans = CategorizeEmphasisSpans(nestedSpans);
      if (categorizedNestedSpans == null || categorizedNestedSpans.Count == 0) {
        return (nextSearchStartIdx, null);
      }

      var filteredSpans = RemoveInnerSpansNotChangingEmphasis(categorizedNestedSpans);
      List<FormattedFragment> flattenedFragments = FlattenNestedEmphasisSpans(filteredSpans);
      var fragmentsInGroups = flattenedFragments.Select(f => new FormattedFragmentGroup(new List<FormattedFragment>() { f })).ToList();
      return (nextSearchStartIdx, fragmentsInGroups);
    }


    private static (int nextSearchStartIdx, List<EmphasisSpan> nestedSpans) FindNextEmphasisBlock(
        string text, int searchStartIdx, int searchEndIdx)
    {
      List<EmphasisSpan> nestedSpans = null;

      while (searchStartIdx < searchEndIdx) {
        (int nextInnerSearchStartIdx, EmphasisSpan? innerSpan) = FindEmphasis(text, searchStartIdx, searchEndIdx);
        if (nextInnerSearchStartIdx < 0) {
          Debug.Assert(innerSpan == null);
          break;
        }

        Debug.Assert(nextInnerSearchStartIdx > searchStartIdx);
        Debug.Assert(nextInnerSearchStartIdx <= searchEndIdx);
        if (innerSpan == null) {
          searchStartIdx = nextInnerSearchStartIdx;
          continue;
        }

        nestedSpans = nestedSpans ?? new List<EmphasisSpan>();
        nestedSpans.Add(innerSpan.Value);
        searchStartIdx = nextInnerSearchStartIdx;
        searchEndIdx = innerSpan.Value.EndEmphasisStartIdx;
      }

      int nextSearchStartIdx = nestedSpans?.Last().EndEmphasisEndIdx ?? -1;
      return (nextSearchStartIdx, nestedSpans);
    }


    private static (int nextSearchStartIdx, EmphasisSpan? foundSpan) FindEmphasis(
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
        int endEmphasisStartIdx = text.IndexOf(emphasisMarker, searchStartIdx, StringComparison.InvariantCulture);
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


    private static List<CategorizedEmphasisSpan> CategorizeEmphasisSpans(List<EmphasisSpan> nestedSpans)
    {
      Debug.Assert(nestedSpans != null && nestedSpans.Count > 0);

      var categorizedSpans = new List<CategorizedEmphasisSpan>();
      bool strikethrough = false;
      int emphasisLevel = 0;
      for (int nestingIdx = 0; nestingIdx < nestedSpans.Count; ++nestingIdx) {
        EmphasisSpan span = nestedSpans[nestingIdx];
        switch (span.EmphasisMarker) {
          case "~~":
            if (nestingIdx > 0) {
              // Strikethrough cannot be nested inside other emphasis in Doxygen.
              return null;
            }
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
            continue; // Skip this span, it contains an unsupported marker.
        }

        categorizedSpans.Add(new CategorizedEmphasisSpan() {
          Span = span,
          IsStrikethrough = strikethrough,
          EmphasisLevel = emphasisLevel
        });
      }

      return categorizedSpans;
    }


    private static List<CategorizedEmphasisSpan> RemoveInnerSpansNotChangingEmphasis(
      List<CategorizedEmphasisSpan> categorizedNestedSpans)
    {
      Debug.Assert(categorizedNestedSpans != null && categorizedNestedSpans.Count > 0);

      var filteredSpans = new List<CategorizedEmphasisSpan> {
        categorizedNestedSpans[0]
      };

      for (int i = 1; i < categorizedNestedSpans.Count; ++i) {
        var currentSpan = categorizedNestedSpans[i];
        var prevSpan = filteredSpans.Last();
        Debug.Assert(currentSpan.Span.StartEmphasisStartIdx >= prevSpan.Span.StartEmphasisEndIdx);
        Debug.Assert(currentSpan.Span.EndEmphasisEndIdx <= prevSpan.Span.EndEmphasisStartIdx);
        if (currentSpan.EmphasisLevel != prevSpan.EmphasisLevel
            || currentSpan.IsStrikethrough != prevSpan.IsStrikethrough) {
          filteredSpans.Add(currentSpan);
        }
      }

      return filteredSpans;
    }


    private static List<FormattedFragment> FlattenNestedEmphasisSpans(
        List<CategorizedEmphasisSpan> categorizedNestedSpans)
    {
      Debug.Assert(categorizedNestedSpans != null && categorizedNestedSpans.Count > 0);
      var flattenedSpans = new List<FormattedFragment>();

      for (int i = 0; i < categorizedNestedSpans.Count - 1; ++i) {
        CategorizedEmphasisSpan currentSpan = categorizedNestedSpans[i];
        CategorizedEmphasisSpan nestedSpan = categorizedNestedSpans[i + 1];

        if (currentSpan.Span.StartEmphasisEndIdx == nestedSpan.Span.StartEmphasisStartIdx
            && currentSpan.Span.EndEmphasisStartIdx == nestedSpan.Span.EndEmphasisEndIdx) {
          nestedSpan.Span.StartEmphasisStartIdx = currentSpan.Span.StartEmphasisStartIdx;
          nestedSpan.Span.EndEmphasisEndIdx = currentSpan.Span.EndEmphasisEndIdx;
          nestedSpan.Span.EmphasisMarker = currentSpan.Span.EmphasisMarker + nestedSpan.Span.EmphasisMarker;
          categorizedNestedSpans[i + 1] = nestedSpan;
          continue;
        }

        ClassificationEnum? currentClassification = GetClassificationFor(currentSpan);
        if (currentClassification == null) {
          continue;
        }

        flattenedSpans.Add(new FormattedFragment(
            startIndex: currentSpan.Span.StartEmphasisStartIdx,
            length: nestedSpan.Span.StartEmphasisStartIdx - currentSpan.Span.StartEmphasisStartIdx,
            classification: currentClassification.Value));
        flattenedSpans.Add(new FormattedFragment(
            startIndex: nestedSpan.Span.EndEmphasisEndIdx,
            length: currentSpan.Span.EndEmphasisEndIdx - nestedSpan.Span.EndEmphasisEndIdx,
            classification: currentClassification.Value));
      }

      CategorizedEmphasisSpan innermostSpan = categorizedNestedSpans.Last();
      ClassificationEnum? innermostClassification = GetClassificationFor(innermostSpan);
      if (innermostClassification.HasValue) {
        flattenedSpans.Add(new FormattedFragment(
            startIndex: innermostSpan.Span.StartEmphasisStartIdx,
            length: innermostSpan.Span.EndEmphasisEndIdx - innermostSpan.Span.StartEmphasisStartIdx,
            classification: innermostClassification.Value));
      }

      flattenedSpans.Sort((lhs, rhs) => lhs.StartIndex.CompareTo(rhs.StartIndex));

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


    private static ClassificationEnum? GetClassificationFor(CategorizedEmphasisSpan categorizedEmphasisSpan)
    {
      if (categorizedEmphasisSpan.IsStrikethrough) {
        // TODO: Combination with emphasis levels
        return ClassificationEnum.Strikethrough;
      }
      switch (categorizedEmphasisSpan.EmphasisLevel) {
        case 1:
          return ClassificationEnum.EmphasisMinor;
        case 2:
          return ClassificationEnum.EmphasisMajor;
        case 3:
          return ClassificationEnum.EmphasisHuge;
        default:
          // More than level 3 is not supported by Doxygen.
          return null;
      }
    }


    private static readonly List<FormattedFragmentGroup> cEmptyList = new List<FormattedFragmentGroup>();
  }
}
