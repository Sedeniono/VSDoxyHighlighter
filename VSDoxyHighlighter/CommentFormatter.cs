﻿using EnvDTE90;
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
    Strikethrough,
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
            "arg", "li", "docbookonly", "htmlonly", @"htmlonly\[block\]", "latexonly", "manonly",
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
        re = new Regex(BuildRegex_KeywordAnywhere_WhitespaceAfterwardsRequiredButNoParam(new string[] {
            @"fileinfo\{file\}", @"fileinfo\{extension\}", @"fileinfo\{filename\}",
            @"fileinfo\{directory\}", @"fileinfo\{full\}", 
            "lineinfo", "endlink", "endcode", "enddocbookonly", "enddot", "endmsc", 
            "enduml", "endhtmlonly", "endlatexonly", "endmanonly", "endrtfonly",
            "endverbatim", "endxmlonly", "n"
          }), cOptions),
        types = Tuple.Create(FormatTypes.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam(new string[] {
            @"f\$", @"f\(", @"f\)", @"f\[", @"f\]", @"f\}",
            @"\@", @"\&", @"\$", @"\#", @"\<", @"\>", @"\%", @"\.", @"\=", @"\::", @"\|",
            @"\---", @"\--"
          }), cOptions),
        types = Tuple.Create(FormatTypes.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_FormulaEnvironmentStart(), cOptions),
        types = Tuple.Create(FormatTypes.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_Language(), cOptions),
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
        // (1)  Stuff allowed to precede the first "*". According to the doxygen documentation:
        //      Only the following is allowed: a space, newline, or one the following characters <{([,:;
        // (2a) Match the actual starting "*"
        // (2b) After the "*", some characters are forbidden. Another "*" is forbidden, so that we can detect **bold** text.
        //      Space and tab are forbidden to reduce the number of false positives, especially until we implement reliable
        //      classification of code vs comment (in "* str*" the "str" is not formatted because of the space).
        //      We also forbid a ")" to rule out constructs in the code such as: int * (*)(const char*)
        // (2c) Match any character multiple times, but not those which are preceded by whitesapce or "*".
        // (2d) Before the terminating "*", some characters must NOT appear.
        //      The "*" is ruled out so that we can detect **bold** text with the other regex below.
        //      "/" is forbidden since "/*" would be a comment start.
        //      Otherwise, the doxygen documentation states, that the following is NOT allowed:
        //      space, newline, or one the following characters ({[<=+-\@
        // (2e) Match the actual terminating "*"
        // (3)  After the terminating "*", not everything is allowed. According to the doxgen documentation,
        //      only non-alphanumeric characters are allowed. We also forbid "*" for proper support of **bold** text,
        //      similar for "~" and "_". Also, "/" is not allowed to not match the C-style comment terminator "*/".
        //      We also forbid "<" and ">" to rule out some false positives in C++ templates (until we implemented detection
        //      of whether we are actually in a comment or in code).
        // 
        //                        1           2a   2b          2c                      2d               2e            3
        //                __________________  __ ________  _________________ __________________________ __ ____________________________
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(\*[^\* \t\)](?:.(?![ \t]\*))*?[^\*\/ \t\n\r\({\[<=\+\-\\@]\*)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMinor)
      });

      // **bold**
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(\*\*[^\* \t\)](?:.(?![ \t]\*))*?[^\*\/ \t\n\r\({\[<=\+\-\\@]\*\*)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMajor)
      });

      // _italic_
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(_[^_ \t\)](?:.(?![ \t]_))*?[^_\/ \t\n\r\({\[<=\+\-\\@]_)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMinor)
      });

      // __bold__
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(__[^_ \t\)](?:.(?![ \t]_))*?[^_\/ \t\n\r\({\[<=\+\-\\@]__)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions),
        types = Tuple.Create(FormatTypes.EmphasisMajor)
      });

      // ~~strikethrough~~
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(~~[^~ \t\)](?:.(?![ \t]~))*?[^~\/ \t\n\r\({\[<=\+\-\\@]~~)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions),
        types = Tuple.Create(FormatTypes.Strikethrough)
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

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_StartUmlCommandWithBracesOptions(), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Title, FormatTypes.Parameter, FormatTypes.Parameter)
      });

      //----- More parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_1OptionalCaption_1OptionalSizeIndication(new string[] {
          "dot", "msc",
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

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_ImageCommand(), cOptions),
        types = (FormatTypes.NormalKeyword, FormatTypes.Parameter, FormatTypes.Parameter, FormatTypes.Title, FormatTypes.Parameter, FormatTypes.Parameter)
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

    // Regex to ensure that whitespace or a new line or the end of the string follows after some command.
    // Using "\b" is insufficient.
    private const string cWhitespaceAfterwards = @"(?:$|[ \t\n\r])";


    private string BuildRegex_KeywordAtLineStart_NoParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords})){cWhitespaceAfterwards}";
    }

    private string BuildRegex_CodeCommand()
    {
      // Command \code, \code{cpp}, ...
      // https://www.doxygen.nl/manual/starting.html#step1
      string validFileExtensions = @"unparsed|dox|doc|c|cc|cxx|cpp|c\+\+|ii|ixx|ipp|i\+\+|inl|h|H|hh|HH|hxx|hpp|h\+\+|mm|txt|idl|ddl|odl|java|cs|d|php|php4|php5|inc|phtml|m|M|py|pyw|f|for|f90|f95|f03|f08|f18|vhd|vhdl|ucf|qsf|l|md|markdown|ice";
      return $@"{cCommentStart}({cCmdPrefix}code(?:\{{\.(?:{validFileExtensions})\}})?){cWhitespaceAfterwards}";
    }

    private string BuildRegex_KeywordAnywhere_WhitespaceAfterwardsRequiredButNoParam(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"({cCmdPrefix}(?:{concatKeywords})){cWhitespaceAfterwards}";
    }

    private string BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam(string[] keywords) 
    {
      string concatKeywords = String.Join("|", keywords);
      return $@"({cCmdPrefix}(?:{concatKeywords}))";
    }

    private string BuildRegex_FormulaEnvironmentStart() 
    {
      return $@"({cCmdPrefix}f\{{.*\}}\{{?)";
    }

    private string BuildRegex_Language() 
    {
      return $@"({cCmdPrefix}~(?:[^ \t]\w+)?)";
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
      return $@"\B({cCmdPrefix}(?:{concatKeywords}))[ \t]+([^ \t\n\r]+)";
    }

    private string BuildRegex_KeywordSomewhereInLine_1ParamAsWord_1OptionalQuotedParam(string[] keywords)
    {
      // Examples:
      //   \ref Class::Func()
      //   Text \ref Class.Func() some text
      //   Text \ref subsection1. The point is not part of the parameter.
      //   \ref func(double, int) should match also match the double and int and also the parantheses.
      //   (\ref func()) should not match the final paranthesis (and also of course not the opening one).
      string concatKeywords = String.Join("|", keywords);

      // https://regex101.com/r/mQrhj8/1
      // (1) matches the first parameter to \ref.
      //    First part: Match stuff before potential parantheses
      //       (1a): Match any word character
      //       (1b): But also match "::" and ".". However, we only want to do this if afterwards whitespace comes.
      //             Otherwise, we have an ordinary punctuation character instead of a C++ indirection.
      //             I.e. match the point in "@ref Class.func" but not in "See some @ref class. More text".
      //    Second part (1c): Match optionally available parantheses, including everything between.
      //        To keep things simple, we do not match balanced parantheses; nesting should never happen in this context.
      //        Thus, we simply take the next ")" after the opening "(".
      // (2) Match everything between successive quotes.
      //                                                     1a           1b                        1c                2
      //                                                  ((____|{__________________________})+____________) __________________________       
      return $@"\B({cCmdPrefix}(?:{concatKeywords}))[ \t]+((?:\w|(?:(?:::)|\.(?=[^: \t\n\r])))+(?:\(.*?\))?)(?:[ \t]+(""[^\r\n]*?""))?";
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
      return $@"({cCmdPrefix}startuml(?:{{.*?}})?){cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    private const string cRegexForOptionalFileWithOptionalQuotes =
      // (1) and (2) together match the 
      // (1) skip whitespace    
      // (2a) Match quotes, allowing whitespace between the quotes
      // (2b) OR: Match everything till the next white space (no quotes)
      //    1          2a                 2b
      //   _____  _________________|_______________
      @"(?:[ \t]+((?:""[^\r\n]*?"")|(?:[^ \t\r\n]*)))?";

    private string BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication(string[] keywords) 
    {
      string concatKeywords = String.Join("|", keywords);
      // Examples:
      //   Without quotes: @dotfile filename    "foo test" width=200cm height=1cm
      //      With quotes: @dotfile "file name" "foo test" width=200cm height=1cm
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b{cRegexForOptionalFileWithOptionalQuotes}{cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    private string BuildRegex_ImageCommand()
    {                                          
      return $@"({cCmdPrefix}image(?:{{.*?}})?)[ \t]+(html|latex|docbook|rtf|xml)\b{cRegexForOptionalFileWithOptionalQuotes}{cRegex_1OptionalCaption_1OptionalSizeIndication}";
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
