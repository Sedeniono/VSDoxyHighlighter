using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;


namespace VSDoxyHighlighter
{


  /// <summary>
  /// Represents how a single continuous piece of text should be formatted.
  /// </summary>
  [DebuggerDisplay("StartIndex={StartIndex}, Length={Length}, Classification={Classification}")]
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
    /// The text fragment should be formatted according to this classification.
    /// </summary>
    public ClassificationEnum Classification { get; private set; }

    /// <summary>
    /// The index of the last formatted character.
    /// </summary>
    public int EndIndex
    {
      get { return Math.Max(StartIndex + Length - 1, StartIndex); }
    }

    public FormattedFragment(int startIndex, int length, ClassificationEnum classification)
    {
      Debug.Assert(startIndex >= 0);
      Debug.Assert(length >= 0);

      StartIndex = startIndex;
      Length = length;
      Classification = classification;
    }

    public override bool Equals(object obj)
    {
      if (!(obj is FormattedFragment casted)) {
        return false;
      }

      return
        StartIndex == casted.StartIndex
        && Length == casted.Length
        && Classification == casted.Classification;
    }

    public override int GetHashCode()
    {
      return Tuple.Create(StartIndex, Length, Classification).GetHashCode();
    }
  }


  /// <summary>
  /// Provides facilities to format the doxygen comments in a piece of source code.
  /// This implements the main logic to find the fragments that should be formatted. It
  /// is independent of Visual Studio services, and thus can be easily unit tested.
  /// 
  /// Note that it does NOT identify which pieces of some text are located in comments
  /// and which lie outside of it. Rather, it expects only the text in comments as input.
  /// </summary>
  public class CommentFormatter
  {
    public CommentFormatter(List<DoxygenCommandGroup> doxygenCommands) 
    {
      mDoxygenCommandGroups = doxygenCommands;
      mMatchers = BuildMatchers(doxygenCommands);
    }


    /// <summary>
    /// Given some code in "text", returns a list of fragments that specifies how the comments
    /// in the "text" should be formatted.
    /// NOTE: The logic includes only rudimentary and incomplete checks whether some piece is a 
    /// comment or not. Therefore, the input "text" should consist of comments. This is true
    /// when called in Visual Studio, because there we filter out non-comments before calling this
    /// function. In the automated tests, however, this does not happen; cf. SpanSplitter().
    /// </summary>
    /// <param name="text">This whole text is formatted.</param>
    /// <returns>A list of fragments that point into the given "text" and which should be formatted.
    /// FormattedFragment.startIndex==0 means the first character in the input "text".</returns>
    public SortedSet<FormattedFragment> FormatText(string text)
    {
      text = text.TrimEnd();
      if (text.EndsWith("*/")) {
        // Strip terminating "*/" so that it is not highlighted in commands such as
        //     /** @ingroup foo */
        // I.e. in commands whose parameter stretches till the end of the line.
        // Removing it beforehand is easier than adapting the regex.
        text = text.Substring(0, text.Length - 2);
      }

      // Note SortedSet: If there are multiple fragments that overlap, the first regex wins.
      var result = new SortedSet<FormattedFragment>(new NonOverlappingFragmentsComparer());

      foreach (var matcher in mMatchers) {
        var foundMatches = matcher.re.Matches(text);
        foreach (Match m in foundMatches) {
          if (1 < m.Groups.Count && m.Groups.Count <= matcher.types.Length + 1) {
            for (int idx = 0; idx < m.Groups.Count - 1; ++idx) {
              Group group = m.Groups[idx + 1];
              if (group.Success && group.Captures.Count == 1 && group.Length > 0) {
                FragmentType fragmentType = (FragmentType)matcher.types[idx];
                string fragmentText = text.Substring(group.Index, group.Length);
                ClassificationEnum? classification = FindClassificationEnumForFragment(mDoxygenCommandGroups, fragmentType, fragmentText);
                if (classification != null) {
                  result.Add(new FormattedFragment(group.Index, group.Length, classification.Value));
                }
              }
            }
          }
        }
      }

      return result;
    }


    private static ClassificationEnum? FindClassificationEnumForFragment(List<DoxygenCommandGroup> knownCommands, FragmentType fragmentType, string fragmentText)
    {
      switch (fragmentType) {
        case FragmentType.Command:
          if (fragmentText.Length > 0) {
            // Strip the initial "\" or "@".
            string commandWithoutStart = fragmentText.Substring(1);

            // TODO: SLOW. Use a dictionary???
            int commandGroupIdx = knownCommands.FindIndex(group => group.Commands.Contains(commandWithoutStart));
            if (commandGroupIdx < 0) {
              // Some commands such as "\code" come with special regex parsers that attach addition parameters directly to the command.
              // For example, we get as fragmentText "\code{.py}" here. So if we couldn't match it exactly, check for matching start.
              commandGroupIdx = knownCommands.FindIndex(
                group => group.Commands.FindIndex(origCmd => commandWithoutStart.StartsWith(origCmd)) >= 0);
            }

            if (commandGroupIdx >= 0) {
              DoxygenCommandType cmdType = knownCommands[commandGroupIdx].DoxygenCommandType;
              switch (cmdType) {
                case DoxygenCommandType.Command1:
                  return ClassificationEnum.Command1;
                case DoxygenCommandType.Command2:
                  return ClassificationEnum.Command2;
                case DoxygenCommandType.Command3:
                  return ClassificationEnum.Command3;
                case DoxygenCommandType.Note:
                  return ClassificationEnum.Note;
                case DoxygenCommandType.Warning:
                  return ClassificationEnum.Warning;
                case DoxygenCommandType.Exceptions:
                  return ClassificationEnum.Exceptions;
                default:
                  throw new Exception($"Unknown DoxygenCommandType: {cmdType}");
              }
            }
          }
          return null;

        case FragmentType.Parameter1:
          return ClassificationEnum.Parameter1;
        case FragmentType.Parameter2:
          return ClassificationEnum.Parameter2;
        case FragmentType.Title:
          return ClassificationEnum.Title;
        case FragmentType.EmphasisMinor:
          return ClassificationEnum.EmphasisMinor;
        case FragmentType.EmphasisMajor:
          return ClassificationEnum.EmphasisMajor;
        case FragmentType.Strikethrough: 
          return ClassificationEnum.Strikethrough;
        case FragmentType.InlineCode:
          return ClassificationEnum.InlineCode;
        default:
          throw new Exception($"Unknown fragment type: {fragmentType}");
      }
    }


    private static List<FragmentMatcher> BuildMatchers(List<DoxygenCommandGroup> doxygenCommands)
    {
      var matchers = new List<FragmentMatcher>();

      // NOTE: The order in which the regexes are created and added here matters!
      // If there is more than one regex matching a certain text fragment, the first one wins.

      // `inline code`
      // Note: Right at the start to overwrite all others.
      matchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(`.*?`)", cOptions, cRegexTimeout),
        types = Tuple.Create(FragmentType.InlineCode)
      });

      // Add all Doxygen commands
      foreach (DoxygenCommandGroup cmdGroup in doxygenCommands) {
        var escapedCommands = cmdGroup.Commands.ConvertAll(s => Regex.Escape(s));
        matchers.Add(new FragmentMatcher {
          re = new Regex(cmdGroup.RegexCreator(escapedCommands), cOptions),
          types = cmdGroup.FragmentTypes
        });
      }

      // *italic*
      matchers.Add(new FragmentMatcher
      {
        // https://regex101.com/r/ekhlTW/1
        // (1)  Stuff allowed to precede the first "*". According to the doxygen documentation:
        //      Only the following is allowed: a space, newline, or one the following characters <{([,:;
        // (2a) Match the actual starting "*"
        // (2b) After the "*", some characters are forbidden. Another "*" is forbidden, so that we can detect **bold** text.
        //      Space and tab are forbidden to reduce the number of false positives, especially until we implement reliable
        //      classification of code vs comment (in "* str*" the "str" is not formatted because of the space).
        //      We also forbid a ")" to rule out constructs in the code such as: int * (*)(const char*)
        // (2c) Match any character multiple times, but not those which are preceded by whitespace or "*".
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
        //                        1           2a     2b               2c                   2d               2e            3
        //                __________________  __ ____________ _________________ __________________________ __ ____________________________
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(\*(?![\* \t\)])(?:.(?![ \t]\*))*?[^\*\/ \t\n\r\({\[<=\+\-\\@]\*)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FragmentType.EmphasisMinor)
      });

      // **bold**
      matchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(\*\*(?![\* \t\)])(?:.(?![ \t]\*))*?[^\*\/ \t\n\r\({\[<=\+\-\\@]\*\*)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FragmentType.EmphasisMajor)
      });

      // _italic_
      matchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(_(?![_ \t\)])(?:.(?![ \t]_))*?[^_\/ \t\n\r\({\[<=\+\-\\@]_)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FragmentType.EmphasisMinor)
      });

      // __bold__
      matchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(__(?![_ \t\)])(?:.(?![ \t]_))*?[^_\/ \t\n\r\({\[<=\+\-\\@]__)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FragmentType.EmphasisMajor)
      });

      // ~~strikethrough~~
      matchers.Add(new FragmentMatcher
      {
        re = new Regex(@"(?:^|[ \t<{\(\[,:;])(~~(?![~ \t\)])(?:.(?![ \t]~))*?[^~\/ \t\n\r\({\[<=\+\-\\@]~~)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        types = Tuple.Create(FragmentType.Strikethrough)
      });


      return matchers;
    }


    // Regex to match where a command that should only appear at the start of a comment line is allowed to start.
    // It is allowed to start after "/*", "/*!", "/**", "//", "///", "//!" and also at the start of the string (i.e. the
    // start of the line, since we get always whole lines). Moreover, we skip any "*" that come after these
    // starting markers.
    //                             string start| ///  | //! | // | /* | /*! | /**          v Skip any "*" at the start of the comment.
    private const string cCommentStart = @"(?:^|\/\/\/|\/\/!|\/\/|\/\*|\/\*!|\/\*\*)[ \t]*\**[ \t]*";

    // Most of the commands start with a "@" or "\". This is the regex to match the beginning.
    private const string cCmdPrefix = @"(?:@|\\)";

    // Regex to ensure that whitespace or a new line or the end of the string follows after some command.
    // Using "\b" is insufficient.
    private const string cWhitespaceAfterwards = @"(?:$|[ \t\n\r])";


    public static string BuildRegex_KeywordAtLineStart_NoParam(ICollection<string> keywords)
    {
      string concatKeywords = string.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords})){cWhitespaceAfterwards}";
    }

    // https://www.doxygen.nl/manual/starting.html#step1
    public static string[] cCodeFileExtensions = { 
      "unparsed", "dox", "doc", "c", "cc", "cxx", "cpp", "c++", "ii", "ixx", "ipp", "i++", "inl", "h", "H", "hh", "HH", "hxx", "hpp", "h++", 
      "mm", "txt", "idl", "ddl", "odl", "java", "cs", "d", "php", "php4", "php5", "inc", "phtml", "m", "M", "py", "pyw", 
      "f", "for", "f90", "f95", "f03", "f08", "f18", "vhd", "vhdl", "ucf", "qsf", "l", "md", "markdown", "ice" };

    public static string BuildRegex_CodeCommand(ICollection<string> keywords)
    {
      // Command \code, \code{cpp}, ...
      string concatKeywords = string.Join("|", keywords);
      string validFileExtensions = string.Join("|", cCodeFileExtensions);
      validFileExtensions = validFileExtensions.Replace("+", @"\+");
      return $@"({cCmdPrefix}{concatKeywords}(?:\{{\.(?:{validFileExtensions})\}})?){cWhitespaceAfterwards}";
    }

    public static string BuildRegex_KeywordAnywhere_WhitespaceAfterwardsRequiredButNoParam(ICollection<string> keywords)
    {
      string concatKeywords = string.Join("|", keywords);
      return $@"({cCmdPrefix}(?:{concatKeywords})){cWhitespaceAfterwards}";
    }

    public static string BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam(ICollection<string> keywords) 
    {
      string concatKeywords = string.Join("|", keywords);
      return $@"({cCmdPrefix}(?:{concatKeywords}))";
    }

    public static string BuildRegex_FormulaEnvironmentStart(ICollection<string> keywords) 
    {
      string concatKeywords = string.Join("|", keywords);
      return $@"({cCmdPrefix}{concatKeywords}\{{.*\}}\{{?)";
    }

    public static string BuildRegex_Language(ICollection<string> keywords) 
    {
      string concatKeywords = string.Join("|", keywords);
      return $@"({cCmdPrefix}{concatKeywords}(?:[^ \t]\w+)?)";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(ICollection<string> keywords)
    {
      string concatKeywords = string.Join("|", keywords);

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

    public static string BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/yCZkWA/1
      string concatKeywords = string.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+([^\n\r]+)?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd(ICollection<string> keywords)
    {
      // BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd() also treats the 1 parameter as optional to provide
      // early syntax highlighting (if the parameter does not yet exist). Neverthless, we need a different regex
      // for the optional parameter. Reason:
      //   \param: MyParameter  --> The ":" is invalid syntax, and nothing should be formatted. Doxygen complains.
      //   \par: My paragraph  --> The title of the paragraph is ": My paragraph". Probably not what the user intended,
      //                           but nevertheless doxygen parses it that way.
      string concatKeywords = string.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))\b(?:[ \t]*([^\n\r]*))?";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/qaaWBO/1
      string concatKeywords = string.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+([^ \t\n\r]+)?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/8QcyXW/1
      string concatKeywords = string.Join("|", keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+(""[^\r\n]*?"")?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/Z7R3xS/1
      string concatKeywords = string.Join("|", keywords);

      // (1) Match the required word. As noted before, we actually treat it as optional.
      // (2) Optional word
      // (3) Optional parameter till the end
      // (4) In case (1-3) did not match anything, match the end of line or string, so that the keyword is highlighted even without parameters.
      //                                                                           1                    2                     3                4
      //                                                            (        ________________ _______________________ ____________________) ________
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+(\w[^ \t\n\r]*)?(?:[ \t]+([^ \t\n\r]*))?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/fCM8p7/1
      string concatKeywords = string.Join("|", keywords);
      return $@"\B({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+([^ \t\n\r]*)?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWordOrQuoted(ICollection<string> keywords)
    {
      // https://regex101.com/r/yxbTV1/1
      string concatKeywords = string.Join("|", keywords);
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b(?:(?:[ \t]*((?:""[^""]*"")|(?:(?<=[ \t])[^ \t\n\r]*))?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord_1OptionalQuotedParam(ICollection<string> keywords)
    {
      // Examples:
      //   \ref Class::Func()
      //   Text \ref Class.Func() some text
      //   Text \ref subsection1. The point is not part of the parameter.
      //   \ref func(double, int) should match also match the double and int and also the parantheses.
      //   (\ref func()) should not match the final paranthesis (and also of course not the opening one).
      string concatKeywords = string.Join("|", keywords);

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

    public const string cRegex_1OptionalCaption_1OptionalSizeIndication =
      //| Optional quoted caption | Optional width              | Optional height             |
      //|_________________________|_____________________________|_____________________________| 
      @"(?:[ \t]+(""[^\r\n]*?""))?(?:[ \t]+(width=[^ \t\r\n]*))?(?:[ \t]+(height=[^ \t\r\n]*))?";

    public static string BuildRegex_1OptionalCaption_1OptionalSizeIndication(ICollection<string> keywords) 
    {
      string concatKeywords = string.Join("|", keywords);
      // Example: \dot "foo test"  width=2\textwidth   height=1cm
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b{cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    public static string BuildRegex_StartUmlCommandWithBracesOptions(ICollection<string> keywords) 
    {
      string concatKeywords = string.Join("|", keywords);
      return $@"({cCmdPrefix}{concatKeywords}(?:{{.*?}})?){cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    private const string cRegexForOptionalFileWithOptionalQuotes =
      // (1) and (2) together match the 
      // (1) skip whitespace    
      // (2a) Match quotes, allowing whitespace between the quotes
      // (2b) OR: Match everything till the next white space (no quotes)
      //    1          2a                 2b
      //   _____  _________________|_______________
      @"(?:[ \t]+((?:""[^\r\n]*?"")|(?:[^ \t\r\n]*)))?";

    public static string BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication(ICollection<string> keywords) 
    {
      string concatKeywords = string.Join("|", keywords);
      // Examples:
      //   Without quotes: @dotfile filename    "foo test" width=200cm height=1cm
      //      With quotes: @dotfile "file name" "foo test" width=200cm height=1cm
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b{cRegexForOptionalFileWithOptionalQuotes}{cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    public static string BuildRegex_ImageCommand(ICollection<string> keywords)
    {
      string concatKeywords = string.Join("|", keywords);
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/VN43Fy/1
      return $@"({cCmdPrefix}{concatKeywords}(?:{{.*?}})?)(?:(?:[ \t]+(?:(html|latex|docbook|rtf|xml)\b)?{cRegexForOptionalFileWithOptionalQuotes}{cRegex_1OptionalCaption_1OptionalSizeIndication})|[\n\r]|$)";
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


    /// <summary>
    /// Represents one regex that is used to detect a certain type of doxygen command, together
    /// with the appropriate formatting (FormatType) for reach captured group in the regex.
    /// </summary>
    struct FragmentMatcher
    {
      public Regex re { get; set; }

      // One FormatType for each capturing group in the regex.
      public System.Runtime.CompilerServices.ITuple types { get; set; }
    };

    private readonly List<DoxygenCommandGroup> mDoxygenCommandGroups;
    private readonly List<FragmentMatcher> mMatchers;

    private const RegexOptions cOptions = RegexOptions.Compiled | RegexOptions.Multiline;

    // In my tests, each individual regex always used less than 100ms.
    // The max. time I was able to measure for a VERY long line was ~60ms.
    private static readonly TimeSpan cRegexTimeout = TimeSpan.FromMilliseconds(100.0);
  }
}
