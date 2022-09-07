using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;


namespace VSDoxyHighlighter
{
  /// <summary>
  /// Known types of formats. The integer values are used as indices into arrays.
  /// </summary>
  public enum FormatTypes : uint
  {
    NormalKeyword,
    Warning,
    Note,
    Parameter,
    EmphasisMinor,
    EmphasisMajor,
    InlineCode,
    Title
  }


  // Represents the format of a single continuous fragment of text.
  [DebuggerDisplay("StartIndex={StartIndex}, Length={Length}, Type={Type}")]
  public struct FormattedFragment
  {
    /// <summary>
    /// Index where the formatting should start.
    /// </summary>
    public int StartIndex { get; private set; }

    /// <summary>
    /// How many characters to format.
    /// </summary>
    public int Length { get; private set; }

    /// <summary>
    /// The text fragment should be formatted according to this type.
    /// </summary>
    public FormatTypes Type { get; private set; }

    /// <summary>
    /// The index of the last formatted character.
    /// </summary>
    public int EndIndex
    {
      get { return Math.Max(StartIndex + Length - 1, StartIndex); }
    }

    public FormattedFragment(int startIndex, int length, FormatTypes type)
    {
      Debug.Assert(startIndex >= 0);
      Debug.Assert(length >= 0);

      StartIndex = startIndex;
      Length = length;
      Type = type;
    }

    public override bool Equals(object obj)
    {
      if (!(obj is FormattedFragment casted)) {
        return false;
      }

      return
        StartIndex == casted.StartIndex
        && Length == casted.Length
        && Type == casted.Type;
    }

    public override int GetHashCode()
    {
      return Tuple.Create(StartIndex, Length, Type).GetHashCode();
    }
  }


  /// <summary>
  /// Provides facilities to format the doxygen comments in a piece of source code.
  /// </summary>
  public class CommentFormatter
  {
    public CommentFormatter()
    {
      const RegexOptions cOptions = RegexOptions.Compiled | RegexOptions.Multiline;

      mMatchers = new List<FragmentMatcher>();

      // NOTE: The order in which the regexes are created and added here matters!
      // If there is more than one regex matching a certain text fragment, the first one wins.

      //----- Without parameters -------

      // `inline code`
      // Note: Right at the start to overwrite all others.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(`.*?`)", cOptions),
        types = Tuple.Create(FormatTypes.InlineCode)
      });

      // Ordinary keyword
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] { "brief", "details", "see", "return", "returns", "ingroup" }), cOptions),
        types = Tuple.Create(FormatTypes.NormalKeyword)
      });

      // Warning
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] { "warning" }), cOptions),
        types = Tuple.Create(FormatTypes.Warning)
      });

      // Notes
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] { "note", "todo" }), cOptions),
        types = Tuple.Create(FormatTypes.Note)
      });

      // *italic*
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ |\t])(\*[^\* \t](?:.(?![ \t]\*))*?[^\* \t]\*)(?:\r?$|[ |\t])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMinor)
      });

      // **bold**
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ |\t])(\*\*[^\* \t](?:.(?![ \t]\*))*?[^\* \t]\*\*)(?:\r?$|[ |\t])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMajor)
      });

      // _italic_
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ |\t])(_[^_ \t](?:.(?![ \t]_))*?[^_ \t]_)(?:\r?$|[ |\t])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMinor)
      });

      // __bold__
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ |\t])(__[^_ \t](?:.(?![ \t]_))*?[^_ \t]__)(?:\r?$|[ |\t])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMajor)
      });


      //----- With one parameter -------

      // Stuff that can be at the start of lines.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_OneParam(new string[] {
             "param", "tparam", @"param\[in\]", @"param\[out\]", "throw", "throws", "exception", "defgroup"}
             ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });

      // Stuff that can be in the middle of lines.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_OneParam(new string[] {
            "p", "c", "ref" }
            ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });


      //----- With up to two parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_OneRequiredAndOneOptionalTitle(new string[] {
          "addtogroup" }
          ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Title)
      });
    }


    private string BuildRegex_KeywordAtLineStart_NoParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))\b";
    }

    private string BuildRegex_KeywordAtLineStart_OneParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))[ \t]+(\w[^ \t]*)";
    }

    private string BuildRegex_KeywordAtLineStart_OneRequiredAndOneOptionalTitle(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))[ \t]+(\w[^ \t\r\n]*)(?:[ \t]+([^\n\r]*))?";
    }

    private const string cRegexForKeywordAtLineStart = @"(?:^|\/\*|\/\*!|\/\/\/|\/\/!)[ \t]*\**[ \t]*";

    private string BuildRegex_KeywordSomewhereInLine_OneParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return @"\B((?:@|\\)(?:" + concatKeywords + @"))[ \t]+(\w[^ \t]*)";
    }


    /// <summary>
    /// Computes the way the whole provided text should be formatted.
    /// </summary>
    /// <param name="text">This whole text is formatted.</param>
    /// <returns>A list of fragments that point into the given "text" and which should be formatted.</returns>
    public SortedSet<FormattedFragment> FormatText(string text)
    {
      // Note SortedSet: If there are multiple fragments that overlap, the first regex wins.
      var result = new SortedSet<FormattedFragment>(new NonOverlappingFragmentsComparer());

      foreach (var matcher in mMatchers) {
        var foundMatches = matcher.re.Matches(text);
        foreach (Match m in foundMatches) {
          if (1 < m.Groups.Count && m.Groups.Count <= matcher.types.Length + 1) {
            for (int idx = 0; idx < m.Groups.Count - 1; ++idx) {
              Group group = m.Groups[idx + 1];
              if (group.Success && group.Captures.Count == 1) {
                FormatTypes formatType = (FormatTypes)matcher.types[idx];
                result.Add(new FormattedFragment(group.Index, group.Length, formatType));
              }
            }
          }
        }
      }

      return result;
    }


    /// <summary>
    /// Comparer that sorts formatted fragments by their position in the text. 
    /// Overlapping fragments are treated as equal, so that only one can win in the end.
    /// </summary>
    private class NonOverlappingFragmentsComparer : IComparer<FormattedFragment>
    {
      public int Compare(FormattedFragment lhs, FormattedFragment rhs)
      {
        if (lhs.EndIndex < rhs.StartIndex) {
          return -1;
        }
        else if (lhs.StartIndex > rhs.EndIndex) {
          return 1;
        }
        else {
          // Fragments overlap, treat them as equal.
          return 0;
        }
      }
    }


    struct FragmentMatcher
    {
      public Regex re { get; set; }

      // One FormatTypes for each capturing group in the regex.
      public System.Runtime.CompilerServices.ITuple types { get; set; }
    };

    private readonly List<FragmentMatcher> mMatchers;
  }
}
