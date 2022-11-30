using EnvDTE90;
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
  public enum FormatType : uint
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
    public FormatType Type { get; private set; }

    /// <summary>
    /// The index of the last formatted character.
    /// </summary>
    public int EndIndex
    {
      get { return Math.Max(StartIndex + Length - 1, StartIndex); }
    }

    public FormattedFragment(int startIndex, int length, FormatType type)
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
        re = new Regex(@"(`.*?`)", cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.InlineCode)
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
        types = Tuple.Create(FormatType.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_CodeCommand(), cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAnywhere_WhitespaceAfterwardsRequiredButNoParam(new string[] {
            @"fileinfo\{file\}", @"fileinfo\{extension\}", @"fileinfo\{filename\}",
            @"fileinfo\{directory\}", @"fileinfo\{full\}", 
            "lineinfo", "endlink", "endcode", "enddocbookonly", "enddot", "endmsc", 
            "enduml", "endhtmlonly", "endlatexonly", "endmanonly", "endrtfonly",
            "endverbatim", "endxmlonly", "n"
          }), cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam(new string[] {
            @"f\$", @"f\(", @"f\)", @"f\[", @"f\]", @"f\}",
            @"\@", @"\&", @"\$", @"\#", @"\<", @"\>", @"\%", @"\.", @"\=", @"\::", @"\|",
            @"\---", @"\--"
          }), cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_FormulaEnvironmentStart(), cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.NormalKeyword)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_Language(), cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.NormalKeyword)
      });

      // Warning
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] { 
          "warning", "raisewarning"
        }), cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.Warning)
      });

      // Notes
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_NoParam(new string[] { 
          "note", "todo", "attention", "bug", "deprecated"
        }), cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.Note)
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
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(\*[^\* \t\)](?:.(?![ \t]\*))*?[^\*\/ \t\n\r\({\[<=\+\-\\@]\*)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.EmphasisMinor)
      });

      // **bold**
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(\*\*[^\* \t\)](?:.(?![ \t]\*))*?[^\*\/ \t\n\r\({\[<=\+\-\\@]\*\*)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.EmphasisMajor)
      });

      // _italic_
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(_[^_ \t\)](?:.(?![ \t]_))*?[^_\/ \t\n\r\({\[<=\+\-\\@]_)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.EmphasisMinor)
      });

      // __bold__
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(__[^_ \t\)](?:.(?![ \t]_))*?[^_\/ \t\n\r\({\[<=\+\-\\@]__)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.EmphasisMajor)
      });

      // ~~strikethrough~~
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(~~[^~ \t\)](?:.(?![ \t]~))*?[^~\/ \t\n\r\({\[<=\+\-\\@]~~)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FormatType.Strikethrough)
      });

      //----- With one parameter -------

      // Keywords with parameter that must be at the start of lines, parameter terminated by whitespace.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(new string[] {
             "param", "tparam", @"param\[in\]", @"param\[out\]", @"param\[in,out\]", "throw", "throws",
              "exception", "concept", "def", "enum", "extends", "idlexcept", "implements",
              "memberof", "name", "namespace", "package", "relates", "related",
              "relatesalso", "relatedalso", "retval"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter)
      });

      // Keywords with parameter that must be at the start of lines, parameter stretches till the end of the line.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd(new string[] {
             "dir", "example", @"example\{lineno\}", "file", "fn", "ingroup", "overload",
             "property", "typedef", "var", "cond",
             "elseif", "if", "ifnot",
             "dontinclude", "dontinclude{lineno}", 
             "include", "include{lineno}", "include{doc}", "includelineno", "includedoc",
             "line", "skip", "skipline", "until",
             "verbinclude", "htmlinclude", @"htmlinclude\[block\]", "latexinclude",
             "rtfinclude", "maninclude", "docbookinclude", "xmlinclude"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter)
      });

      // Keywords with optional parameter that must be at the start of lines, parameter stretches till the end of the line.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(new string[] {
             "cond"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter)
      });
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(new string[] {
             "par"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Title)
      });

      // Keyword with title
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd(new string[] {
             "mainpage"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Title)
      });

      // Stuff that can be in the middle of lines.
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord(new string[] {
            "p", "c", "anchor", "cite", "link", "refitem", 
            "copydoc", "copybrief", "copydetails", "emoji"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter)
      });
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord(new string[] {
            "a", "e", "em"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.EmphasisMinor)
      });
      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord(new string[] {
            "b"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.EmphasisMajor)
      });


      //----- With up to two parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd(new string[] {
          "addtogroup", "defgroup", "headerfile", "page", "weakgroup",
          "section", "subsection", "subsubsection", "paragraph",
          "snippet", "snippet{lineno}", "snippet{doc}", "snippetlineno", "snippetdoc"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter, FormatType.Title)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd(new string[] {
          "showdate"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter, FormatType.Title)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord_1OptionalQuotedParam(new string[] {
          "ref", "subpage"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter, FormatType.Title)
      });


      //----- With up to three parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd(new string[] {
          "category", "class", "interface", "protocol", "struct", "union"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter, FormatType.Parameter, FormatType.Title)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_StartUmlCommandWithBracesOptions(), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Title, FormatType.Parameter, FormatType.Parameter)
      });

      //----- More parameters -------

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_1OptionalCaption_1OptionalSizeIndication(new string[] {
          "dot", "msc",
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Title, FormatType.Parameter, FormatType.Parameter)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication(new string[] {
          "dotfile", "mscfile", "diafile"
          }), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter, FormatType.Title, FormatType.Parameter, FormatType.Parameter)
      });

      mMatchers.Add(new FragmentMatcher
      {
        re = new Regex(BuildRegex_ImageCommand(), cOptions, cRegexTimeout),
        types = (FormatType.NormalKeyword, FormatType.Parameter, FormatType.Parameter, FormatType.Title, FormatType.Parameter, FormatType.Parameter)
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

    private string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(string[] keywords)
    {
      string concatKeywords = String.Join("|", keywords);

      // Example: "\param[in] myParameter"
      // NOTE: Although the parameter "myParameter" is required, we nevertheless want to highlight the "\param[in]"
      // already before "myParameter" is typed be the user. Thus, although semantically the parameter is required,
      // we make it optional. See the final "?" in the regex part (1).
      //
      // https://regex101.com/r/MKKI71/1
      // Match one of the following 3:
      // (1) First some whitespace, then, if existing, the next word
      // (2) Or: End of line
      // (3) Or: End of string
      //                                                                         1               2    3
      //                                                             _________________________|______|_
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+(\w[^ \t\n\r]*)?)|[\n\r]|$)";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd(string[] keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/yCZkWA/1
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+([^\n\r]+)?)|[\n\r]|$)";
    }

    private string BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(string[] keywords)
    {
      // BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd() also treats the 1 parameter as optional to provide
      // early syntax highlighting (if the parameter does not yet exist). Neverthless, we need a different regex
      // for the optional parameter. Reason:
      //   \param: MyParameter  --> The ":" is invalid syntax, and nothing should be formatted. Doxygen complains.
      //   \par: My paragraph  --> The title of the paragraph is ": My paragraph". Probably not what the user intended,
      //                           but nevertheless doxygen parses it that way.
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))\b(?:[ \t]*([^\n\r]*))?";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd(string[] keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/qaaWBO/1
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+([^ \t\n\r]+)?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd(string[] keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/8QcyXW/1
      string concatKeywords = String.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+(""[^\r\n]*?"")?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    private string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd(string[] keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/Z7R3xS/1
      string concatKeywords = String.Join("|", keywords);

      // (1) Match the required word. As noted before, we actually treat it as optional.
      // (2) Optional word
      // (3) Optional parameter till the end
      // (4) In case (1-3) did not match anything, match the end of line or string, so that the keyword is highlighted even without parameters.
      //                                                                           1                    2                     3                4
      //                                                            (        ________________ _______________________ ____________________) ________
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+(\w[^ \t\n\r]*)?(?:[ \t]+([^ \t\n\r]*))?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    private string BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord(string[] keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/fCM8p7/1
      string concatKeywords = String.Join("|", keywords);
      return $@"\B({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+([^ \t\n\r]*)?)|[\n\r]|$)";
    }

    private string BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord_1OptionalQuotedParam(string[] keywords)
    {
      // Examples:
      //   \ref Class::Func()
      //   Text \ref Class.Func() some text
      //   Text \ref subsection1. The point is not part of the parameter.
      //   \ref func(double, int) should match also match the double and int and also the parantheses.
      //   (\ref func()) should not match the final paranthesis (and also of course not the opening one).
      string concatKeywords = String.Join("|", keywords);

      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      //
      // https://regex101.com/r/EVJaKp/1
      // (1) matches the first parameter to \ref.
      //    First part: Match stuff before potential parantheses
      //       (1a): Match any word character
      //       (1b): But also match "::" and ".". However, we only want to do this if afterwards whitespace comes.
      //             Otherwise, we have an ordinary punctuation character instead of a C++ indirection.
      //             I.e. match the point in "@ref Class.func" but not in "See some @ref class. More text".
      //    Second part (1c): Match optionally available parantheses, including everything between.
      //        To keep things simple, we do not match balanced parantheses; nesting should never happen in this context.
      //        Thus, we simply take the next ")" after the opening "(".
      //    (1d) Make the whole previous match (1a+b+c) optional.
      // (2) Match everything between successive quotes, optionally.
      // (3) If (1+2) did not match anything, match the newline or the end of the string. This ensures that we nevertheless
      //     highlight the keyword, even without parameters.
      //                                                           1a           1b                        1c      1d          2                   3
      //                                                        ((____|{__________________________})+____________)_ _________________________  ________
      return $@"\B({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+((?:\w|(?:(?:::)|\.(?=[^: \t\n\r])))+(?:\(.*?\))?)?(?:[ \t]+(""[^\r\n]*?""))?)|[\n\r]|$)";
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
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/VN43Fy/1
      return $@"({cCmdPrefix}image(?:{{.*?}})?)(?:(?:[ \t]+(?:(html|latex|docbook|rtf|xml)\b)?{cRegexForOptionalFileWithOptionalQuotes}{cRegex_1OptionalCaption_1OptionalSizeIndication})|[\n\r]|$)";
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
                FormatType formatType = (FormatType)matcher.types[idx];
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

      // One FormatType for each capturing group in the regex.
      public System.Runtime.CompilerServices.ITuple types { get; set; }
    };

    private readonly List<FragmentMatcher> mMatchers;
    private const RegexOptions cOptions = RegexOptions.Compiled | RegexOptions.Multiline;

    // In my tests, each individual regex always used less than 100ms.
    // The max. time I was able to measure for a VERY long line was ~60ms.
    private readonly TimeSpan cRegexTimeout = TimeSpan.FromMilliseconds(100.0);
  }
}
