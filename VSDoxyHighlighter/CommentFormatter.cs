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

      // Ordinary keyword without highlighted parameter
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] {
          "brief", "details", "see", "return", "author", "authors", "copyright",
          "date", "noop", "else", "endcond", "endif", "invariant",
          "returns", "ingroup", "callgraph",
          "hidecallgraph", "callergraph", "hidecallergraph", "showrefby", "hiderefby",
          "showrefs", "hiderefs", "endinternal",
          @"fileinfo\{file\}", @"fileinfo\{extension\}", @"fileinfo\{filename\}",
          @"fileinfo\{directory\}", @"fileinfo\{full\}",
          "lineinfo", "hideinitializer", "internal", "nosubgrouping", "private",
          "privatesection", "protected", "protectedsection", "public", "publicsection",
          "pure", "showinitializer", "static"
        }), cOptions),
        types = Tuple.Create(FormatTypes.NormalKeyword)
      });

      // Warning
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] { 
          "warning", "raisewarning"
        }), cOptions),
        types = Tuple.Create(FormatTypes.Warning)
      });

      // Notes
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] { 
          "note", "todo", "attention", "bug", "deprecated"
        }), cOptions),
        types = Tuple.Create(FormatTypes.Note)
      });

      // *italic*
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[^a-zA-Z0-9_\*])(\*[^\* \t](?:.(?![ \t]\*))*?[^\* \t\n\r]\*)(?:\r?$|[^a-zA-Z0-9_\*])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMinor)
      });

      // **bold**
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[^a-zA-Z0-9_\*])(\*\*[^\* \t](?:.(?![ \t]\*))*?[^\* \t\n\r]\*\*)(?:\r?$|[^a-zA-Z0-9_\*])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMajor)
      });

      // _italic_
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[^a-zA-Z0-9_\*])(_[^_ \t](?:.(?![ \t]_))*?[^_ \t\n\r]_)(?:\r?$|[^a-zA-Z0-9_\*])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMinor)
      });

      // __bold__
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[^a-zA-Z0-9_\*])(__[^_ \t](?:.(?![ \t]_))*?[^_ \t\n\r]__)(?:\r?$|[^a-zA-Z0-9_\*])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMajor)
      });


      //----- With one parameter -------

      // Keywords with parameter that can be at the start of lines, parameter terminated by whitespace.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1ParamAsWord(new string[] {
             "param", "tparam", @"param\[in\]", @"param\[out\]", @"param\[in,out\]", "throw", "throws",
              "exception", "concept", "def", "enum", "extends", "idlexcept", "implements",
              "memberof", "name", "namespace", "package", "relates", "related",
              "relatesalso", "relatedalso"}
             ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });

      // Keywords with parameter that can be at the start of lines, parameter stretches till the end of the line.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1ParamTillEnd(new string[] {
             "dir", "example", @"example\{lineno\}", "file", "fn", "ingroup", "overload",
             "property", "typedef", "var", "cond",
             "elseif", "if", "ifnot"}
             ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });

      // Keywords with optional parameter that can be at the start of lines, parameter stretches till the end of the line.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(new string[] {
             "cond"}
             ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(new string[] {
             "par"}
             ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Title)
      });

      // Keyword with title
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1ParamTillEnd(new string[] {
             "mainpage"}
             ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Title)
      });

      // Stuff that can be in the middle of lines.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_OneParamAsWord(new string[] {
            "p", "c", "ref" }
            ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });


      //----- With up to two parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd(new string[] {
          "addtogroup", "defgroup", "headerfile", "page", "weakgroup" }
          ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Title)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd(new string[] {
          "showdate" }
          ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Title)
      });

      //----- With up to three parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd(new string[] {
          "category", "class", "interface", "protocol", "struct", "union" }
          ), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Parameter, FormatTypes.Title)
      });
    }


    private string BuildRegex_KeywordAtLineStart_NoParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))[ \t\n\r]";
    }

    // Parameter terminated by whitespace.
    private string BuildRegex_KeywordAtLineStart_1ParamAsWord(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))[ \t]+(\w[^ \t\n\r]*)";
    }

    private string BuildRegex_KeywordAtLineStart_1ParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))[ \t]+([^\n\r]*)";
    }

    private string BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))\b(?:[ \t]*([^\n\r]*))?";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))[ \t]+([^ \t\r\n]+)(?:[ \t]+([^\n\r]*))?";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))[ \t]+(""[^\r\n]*"")(?:[ \t]+([^\n\r]*))?";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return cRegexForKeywordAtLineStart + @"((?:@|\\)(?:" + concatKeywords + @"))[ \t]+([^ \t\r\n]+)(?:[ \t]+([^ \t\n\r]*))?(?:[ \t]+([^\n\r]*))?";
    }

    private const string cRegexForKeywordAtLineStart = @"(?:^|\/\*|\/\*!|\/\/\/|\/\/!)[ \t]*\**[ \t]*";

    private string BuildRegex_KeywordSomewhereInLine_OneParamAsWord(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return @"\B((?:@|\\)(?:" + concatKeywords + @"))[ \t]+([^ \t\n\r]+)";
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
              if (group.Success && group.Captures.Count == 1 && group.Length > 0) {
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
