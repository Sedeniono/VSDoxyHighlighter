using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static Nerdbank.Streams.MultiplexingStream;


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
      mMatchers = new List<FragmentMatcher>();

      // NOTE: The order in which the regexes are created and added here matters!
      // If there is more than one regex matching a certain text fragment, the first one wins.
      //
      // Based on doxygen 1.9.5 (26th August 2022).


      //----- Without parameters -------

      // `inline code`
      // Note: Right at the start to overwrite all others.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(`.*?`)", cOptions),
        types = Tuple.Create(FormatTypes.InlineCode)
      });

      // Ordinary keyword without highlighted parameter at line start
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] {
            "brief", "short", "details", "sa", "see", "result", "return", "returns", 
            "author", "authors", "copyright", "date", "noop", "else", "endcond", "endif", 
            "invariant", "parblock", "endparblock", "post", "pre", "remark", "remarks",
            "since", "test", "version",
            "ingroup", "callgraph",
            "hidecallgraph", "callergraph", "hidecallergraph", "showrefby", "hiderefby",
            "showrefs", "hiderefs", "endinternal",
            "hideinitializer", "internal", "nosubgrouping", "private",
            "privatesection", "protected", "protectedsection", "public", "publicsection",
            "pure", "showinitializer", "static",
            "addindex", "secreflist", "endsecreflist", "tableofcontents",
            "arg", "docbookonly", "htmlonly", @"htmlonly\[block\]", "latexonly", "manonly",
            "rtfonly", "verbatim", "xmlonly"
          }), cOptions),
        types = Tuple.Create(FormatTypes.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_CodeCommand(), cOptions),
        types = Tuple.Create(FormatTypes.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_NoParam(new string[] {
            @"fileinfo\{file\}", @"fileinfo\{extension\}", @"fileinfo\{filename\}",
            @"fileinfo\{directory\}", @"fileinfo\{full\}", 
            "lineinfo", "endlink", "endcode", "enddocbookonly", "enddot", "endmsc", 
            "enduml", "endhtmlonly", "endlatexonly", "endmanonly", "endrtfonly",
            "endverbatim", "endxmlonly"
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
        // (1)  Stuff allowed to precede the first "*"
        //      Note the "^" in the "[^...]": All of these characters may NOT come before.
        //      We roughly say "only whitespace and punctuation characters" may come before. This ensures that we
        //      do not match "*" in the middle of a string, such as "some*string".
        //      Also, we forbid "/" to rule out matching the comment start "/*".
        // (2a) Match the actual starting "*"
        // (2b) After the "*", some characters are forbidden. Another "*" is forbidden, so that we can detect **bold** text.
        //      Space and tab are forbidden to reduce the number of false positives, especially until we implement reliable
        //      classification of code vs comment (in "* str*" the "str" is not formatted because of the space).
        //      We also forbid a ")" to rule out constructs in the code such as: int * (*)(const char*)
        // (2c) Match any character multiple times, but not those which are preceded by whitesapce or "*".
        // (2d) Before the terminating "*", some characters must NOT appear.
        //      The "*" is ruled out so that we can detect **bold** text with the other regex below.
        //      "/*" is forbidden since it is a comment start.
        //      Also, similar to (2b), we forbid whitespace before ("*str *" is not formatted).
        // (2e) Match the actual terminating "*"
        // (3)  After the terminating "*", not everything is allowed. Similar to (1), we mostly allow only whitespace and
        //      punctuation characters. We also forbid "<" and ">" to rule out some false positives in C++ templates.
        // 
        //                        1              2a   2b          2c                2d       2e            3
        //                _____________________  _ _________  _________________ ____________  _ ___________________________
        re = new Regex(@"(?:^|[^a-zA-Z0-9_\*\/])(\*[^\* \t\)](?:.(?![ \t]\*))*?[^\* \t\n\r\/]\*)(?:\r?$|[^a-zA-Z0-9_\*\/<>])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMinor)
      });

      // **bold**
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[^a-zA-Z0-9_\*\/])(\*\*[^\* \t\)](?:.(?![ \t]\*))*?[^\* \t\n\r\/]\*\*)(?:\r?$|[^a-zA-Z0-9_\*\/<>])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMajor)
      });

      // _italic_
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[^a-zA-Z0-9_\*\/])(_[^_ \t\)](?:.(?![ \t]_))*?[^_ \t\n\r\/]_)(?:\r?$|[^a-zA-Z0-9_\*\/<>])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMinor)
      });

      // __bold__
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[^a-zA-Z0-9_\*\/])(__[^_ \t\)](?:.(?![ \t]_))*?[^_ \t\n\r\/]__)(?:\r?$|[^a-zA-Z0-9_\*\/<>])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMajor)
      });


      //----- With one parameter -------

      // Keywords with parameter that must be at the start of lines, parameter terminated by whitespace.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1ParamAsWord(new string[] {
             "param", "tparam", @"param\[in\]", @"param\[out\]", @"param\[in,out\]", "throw", "throws",
              "exception", "concept", "def", "enum", "extends", "idlexcept", "implements",
              "memberof", "name", "namespace", "package", "relates", "related",
              "relatesalso", "relatedalso", "retval"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });

      // Keywords with parameter that must be at the start of lines, parameter stretches till the end of the line.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1ParamTillEnd(new string[] {
             "dir", "example", @"example\{lineno\}", "file", "fn", "ingroup", "overload",
             "property", "typedef", "var", "cond",
             "elseif", "if", "ifnot",
             "dontinclude", "dontinclude{lineno}", 
             "include", "include{lineno}", "include{doc}", "includelineno", "includedoc",
             "line", "skip", "skipline", "until",
             "verbinclude", "htmlinclude", @"htmlinclude\[block\]", "latexinclude",
             "rtfinclude", "maninclude", "docbookinclude", "xmlinclude"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });

      // Keywords with optional parameter that must be at the start of lines, parameter stretches till the end of the line.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(new string[] {
             "cond"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(new string[] {
             "par"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Title)
      });

      // Keyword with title
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1ParamTillEnd(new string[] {
             "mainpage"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Title)
      });

      // Stuff that can be in the middle of lines.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_1ParamAsWord(new string[] {
            "p", "c", "anchor", "cite", "link", "refitem", 
            "copydoc", "copybrief", "copydetails", "emoji"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter)
      });
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_1ParamAsWord(new string[] {
            "a", "e", "em"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.EmphasisMinor)
      });
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_1ParamAsWord(new string[] {
            "b"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.EmphasisMajor)
      });


      //----- With up to two parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd(new string[] {
          "addtogroup", "defgroup", "headerfile", "page", "weakgroup",
          "section", "subsection", "subsubsection", "paragraph",
          "snippet", "snippet{lineno}", "snippet{doc}", "snippetlineno", "snippetdoc"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Title)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd(new string[] {
          "showdate"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Title)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_1ParamAsWord_1OptionalQuotedParam(new string[] {
          "ref", "subpage"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Title)
      });


      //----- With up to three parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd(new string[] {
          "category", "class", "interface", "protocol", "struct", "union"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Parameter, FormatTypes.Title)
      });


      //----- Special stuff -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_StartUmlCommandWithBracesOptions(), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Title, FormatTypes.Parameter, FormatTypes.Parameter)
      });
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_1OptionalCaption_1OptionalSizeIndication(new string[] {
          "dot", "msc",
          "startuml" // Note for startuml: The braces arguments are handled via BuildRegex_StartUmlCommandWithBracesOptions().
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Title, FormatTypes.Parameter, FormatTypes.Parameter)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication(new string[] {
          "dotfile", "mscfile", "diafile"
          }), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Title, FormatTypes.Parameter, FormatTypes.Parameter)
      });
    }


    // Regex to match where a command that should only appear at the start of a comment line is allowed to start.
    // It is allowed to start after "/*", "/*!", "///", "//!" and also at the start of the string (i.e. the
    // start of the line, since we get always whole lines). Moreover, we skip any "*" that come after these
    // starting markers.
    // Note: Of course, this is insufficient to really detect whether some text is code or
    // comment. For this we need to scan the whole file to find e.g. matching "/*" and "*/".
    // But till this is implemented, this is a first approximation.
    //                             string start| /* | /*! | ///  | //!         v Skip any "*" at the start of the comment.
    private const string cCommentStart = @"(?:^|\/\*|\/\*!|\/\/\/|\/\/!)[ \t]*\**[ \t]*";

    // Most of the commands start with a "@" or "\". This is the regex to match the beginning.
    private const string cCmdPrefix = @"(?:@|\\)";


    private string BuildRegex_KeywordAtLineStart_NoParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))[ \t\n\r]";
    }

    private string BuildRegex_CodeCommand()
    {
      // Command \code, \code{cpp}, ...
      // https://www.doxygen.nl/manual/starting.html#step1
      string validFileExtensions = @"unparsed|dox|doc|c|cc|cxx|cpp|c\+\+|ii|ixx|ipp|i\+\+|inl|h|H|hh|HH|hxx|hpp|h\+\+|mm|txt|idl|ddl|odl|java|cs|d|php|php4|php5|inc|phtml|m|M|py|pyw|f|for|f90|f95|f03|f08|f18|vhd|vhdl|ucf|qsf|l|md|markdown|ice";
      return $@"{cCommentStart}({cCmdPrefix}code(?:\{{\.(?:{validFileExtensions})\}})?)[ \t\n\r]";
    }

    private string BuildRegex_KeywordSomewhereInLine_NoParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"\B({cCmdPrefix}(?:{concatKeywords}))[ \t\n\r]";
    }

    // Parameter terminated by whitespace.
    private string BuildRegex_KeywordAtLineStart_1ParamAsWord(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))[ \t]+(\w[^ \t\n\r]*)";
    }

    private string BuildRegex_KeywordAtLineStart_1ParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))[ \t]+([^\n\r]*)";
    }

    private string BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))\b(?:[ \t]*([^\n\r]*))?";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))[ \t]+([^ \t\r\n]+)(?:[ \t]+([^\n\r]*))?";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))[ \t]+(""[^\r\n]*?"")(?:[ \t]+([^\n\r]*))?";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))[ \t]+([^ \t\r\n]+)(?:[ \t]+([^ \t\n\r]*))?(?:[ \t]+([^\n\r]*))?";
    }

    private string BuildRegex_KeywordSomewhereInLine_1ParamAsWord(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"\B((?:@|\\)(?:{concatKeywords}))[ \t]+([^ \t\n\r]+)";
    }

    private string BuildRegex_KeywordSomewhereInLine_1ParamAsWord_1OptionalQuotedParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"\B((?:@|\\)(?:{concatKeywords}))[ \t]+([\w|\(|\)]+)(?:[ \t]+(""[^\r\n]*?""))?";
    }

    private const string cRegex_1OptionalCaption_1OptionalSizeIndication =
      //| Optional quoted caption | Optional width              | Optional height             |
      //|_________________________|_____________________________|_____________________________| 
      @"(?:[ \t]+(""[^\r\n]*?""))?(?:[ \t]+(width=[^ \t\r\n]*))?(?:[ \t]+(height=[^ \t\r\n]*))?";

    private string BuildRegex_1OptionalCaption_1OptionalSizeIndication(string[] keywords) 
    {
      string concatKeywords = String.Join("|", keywords);
      // Example: \dot "foo test"  width=2\textwidth   height=1cm
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b{cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    private string BuildRegex_StartUmlCommandWithBracesOptions() 
    {
      // Note: The version of startuml without braces is handled via BuildRegex_1OptionalCaption_1OptionalSizeIndication().
      return $@"({cCmdPrefix}startuml{{.*?}}){cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    private string BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication(string[] keywords) 
    {
      string concatKeywords = String.Join("|", keywords);
      // Examples:
      //   Without quotes: @dotfile filename    "foo test" width=200cm height=1cm
      //      With quotes: @dotfile "file name" "foo test" width=200cm height=1cm
      // (1) and (2) together match the 
      // (1) skip whitespace    
      // (2a) Match quotes, allowing whitespace between the quotes
      // (2b) OR: Match everything till the next white space (no quotes)
      //                                                1          2a                 2b
      //                                               _____  _________________|_______________
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b(?:[ \t]+((?:""[^\r\n]*?"")|(?:[^ \t\r\n]*)))?{cRegex_1OptionalCaption_1OptionalSizeIndication}";
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
    private const RegexOptions cOptions = RegexOptions.Compiled | RegexOptions.Multiline;
  }
}
