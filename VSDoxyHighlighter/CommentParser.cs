using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;


namespace VSDoxyHighlighter
{
  //==============================================================================================
  // IFragmentsMatcher
  //==============================================================================================

  /// <summary>
  /// Takes some text and matches one or multiple Doxygen commands, markup pieces, etc.
  /// There are multiple matcher instances; a single IFragmentsMatcher typically finds all Doxygen
  /// commands that can be parsed the same way.
  /// </summary>
  public interface IFragmentsMatcher
  {
    IList<FormattedFragmentGroup> FindFragments(string text);
  }


  //==============================================================================================
  // FormattedFragmentGroup
  //==============================================================================================

  /// <summary>
  /// Contains formatting information for a full Doxygen command, including its parameters.
  /// Every Doxygen command (including its parameters) is represented by one instance, where the command 
  /// (including the "\" or "@") is in "Fragments[0]", and the parameters are in the remaining elements.
  /// A piece of markdown text will only have a single entry in "Fragments".
  /// </summary>
  public class FormattedFragmentGroup 
  {
    public IList<FormattedFragment> Fragments{ get; private set; }

    public int StartIndex => Fragments[0].StartIndex;
    public int EndIndex => Fragments[Fragments.Count - 1].EndIndex;
    public int Length => EndIndex - StartIndex + 1;

    public FormattedFragmentGroup(IList<FormattedFragment> fragments) 
    {
      Debug.Assert(fragments != null && fragments.Count() > 0);
      Fragments = fragments;
    }
  }


  //==============================================================================================
  // FormattedFragment
  //==============================================================================================

  /// <summary>
  /// Represents how a single continuous piece of text should be formatted.
  /// I.e. basically represents e.g. a Doxygen command (without its parameters), a single
  /// parameter of a Doxygen command, or a markdown formatted piece of text.
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
    public int EndIndex => Math.Max(StartIndex + Length - 1, StartIndex);

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

    public string GetText(ITextSnapshot snapshot)
    {
      return snapshot.GetText(StartIndex, Length);
    }
  }


  //==============================================================================================
  // CommentParser
  //==============================================================================================

  /// <summary>
  /// Provides facilities to parse some piece of comment text for doxygen commands, markdown, etc.
  /// This implements the main logic to find the fragments that should be formatted. It
  /// is independent of Visual Studio services, and thus can be easily unit tested.
  /// 
  /// Note that it does NOT identify which pieces of some text are located in comments
  /// and which lie outside of it. Rather, it expects only the text in comments as input.
  /// 
  /// In the context of Visual Studio (i.e. not in the context of unit tests), only one instance
  /// should really exist. There is no need to initialize the regex etc multiple times. Thus,
  /// use VSDoxyHighlighterPackage.CommentParser outside of tests.
  /// </summary>
  public class CommentParser : IDisposable
  {
    public CommentParser(DoxygenCommands doxygenCommands) 
    {
      mDoxygenCommands = doxygenCommands;
      mDoxygenCommands.CommandsGotUpdated += OnDoxygenCommandsGotUpdated;

      InitMatchers();
    }


    /// <summary>
    /// Event gets sent when the underlying configuration changes.
    /// </summary>
    public event EventHandler ParsingMethodChanged;


    /// <summary>
    /// Given some code in "text", returns a list of fragments that specifies how the comments
    /// in the "text" should be formatted.
    /// NOTE: The logic includes only rudimentary and incomplete checks whether some piece is a 
    /// comment or not. Therefore, the input "text" should consist of comments. This is true
    /// when called in Visual Studio, because there we filter out non-comments before calling this
    /// function. In the automated tests, however, this does not happen; cf. CommentExtractor().
    /// </summary>
    /// <param name="text">This whole text is formatted.</param>
    /// <returns>A list of fragments that point into the given "text" and which should be formatted.
    /// FormattedFragment.startIndex==0 means the first character in the input "text".</returns>
    public IEnumerable<FormattedFragmentGroup> Parse(string text)
    {
      text = text.TrimEnd();
      if (text.EndsWith("*/")) {
        // Strip terminating "*/" so that it is not highlighted in commands such as
        //     /** @ingroup foo */
        // I.e. in commands whose parameter stretches till the end of the line.
        // Removing it beforehand is easier than adapting the regex.
        text = text.Substring(0, text.Length - 2);
      }

      var allFragmentGroups = new List<FormattedFragmentGroup>();
      foreach (IFragmentsMatcher matcher in mMatchers) {
        allFragmentGroups.AddRange(matcher.FindFragments(text));
      }

      // In case of overlapping fragment groups, let the group win which starts first. This seems like a sensible thing to do.
      // Especially consider markdown nested in e.g. titles. For example, consider the comment:
      //    @par Some @b text and `backtics`
      // Due to the sorting, everything after "@par" gets interpreted as title and the formatting of "@b" and the backtics effectively
      // get ignored.
      // Doxygen itself actually applies formatting also in titles. However, we don't do this at the moment for two reasons: First,
      // to keep it simple. Second, Doxygen's support for such nested formatting seems fragile and maybe not officially supported;
      // for example, applying backtics in page titles causes the html tag "<tt>" to actually appear in the list of pages rather than
      // the formatted text.
      //
      // Using OrderBy() rather than Sort() to get a stable sort: Of two groups that start at the same position, let that one win
      // which was matched by the earlier matcher. At the time of writing this, it should not actually be possible that two matchers
      // return matches that start at the same position, but who knows what the future holds.
      //
      // Also, NOT passing "sorted" directly into the constructed SortedSet because apparently the constructor does not iterate
      // over the input IEnumerable in the correct order.
      var sorted = allFragmentGroups.OrderBy(fragment => fragment.StartIndex);
      var filtered = new SortedSet<FormattedFragmentGroup>(new NonOverlappingCommandGroupsComparer());
      foreach (var fragmentGroup in sorted) { // Add the fragments in the correct order, to let the first one win in case of overlaps
        filtered.Add(fragmentGroup);
      }

      return filtered;
    }


    /// <summary>
    /// Assuming that <paramref name="cmdWithSlashOrAt"/> contains a Doxygen command (including the "/" or "@"),
    /// returns the corresponding classification.
    /// </summary>
    /// <param name="cmdWithSlashOrAt"></param>
    /// <returns></returns>
    public ClassificationEnum GetClassificationForCommand(string cmdWithSlashOrAt)
    {
      Debug.Assert(cmdWithSlashOrAt.StartsWith("\\") || cmdWithSlashOrAt.StartsWith("@"));

      var parsed = Parse(cmdWithSlashOrAt);
      if (parsed.Count() == 1) {
        FormattedFragmentGroup group = parsed.First();
        if (group.Fragments.Count > 0) {
          return group.Fragments[0].Classification;
        }
      }

      Debug.Assert(false); // Unknown Doxygen command?
      return ClassificationEnum.Command;
    }


    public void Dispose()
    {
      if (mDisposed) {
        return;
      }
      mDisposed = true;

      if (mDoxygenCommands != null) {
        mDoxygenCommands.CommandsGotUpdated -= OnDoxygenCommandsGotUpdated;
      }
    }


    private void OnDoxygenCommandsGotUpdated(object sender, EventArgs e)
    {
      InitMatchers();
      ParsingMethodChanged?.Invoke(this, EventArgs.Empty);
    }


    private void InitMatchers() 
    {
      mMatchers = BuildMatchers(mDoxygenCommands.CommandGroups);
    }


    private static List<IFragmentsMatcher> BuildMatchers(List<DoxygenCommandGroup> doxygenCommands)
    {    
      const RegexOptions cOptions = RegexOptions.Compiled | RegexOptions.Multiline;

      var matchers = new List<IFragmentsMatcher>();

      // NOTE: The order in which the matchers are created and added here should not matter. CommentParser.Parse() sorts
      // the found fragments, and in case of overlapping fragments, selects the "appropriate" one.

      // `inline code`
      matchers.Add(new FragmentsMatcherRegex(
        new Regex(@"(`.*?`)", cOptions, cRegexTimeout),
        new ClassificationEnum[] { ClassificationEnum.InlineCode }
      ));

      // Add all Doxygen commands
      foreach (DoxygenCommandGroup cmdGroup in doxygenCommands) {
        matchers.Add(cmdGroup.MatcherFactory.Create(cmdGroup.Commands, cmdGroup.Classifications));
      }

      // *italic*
      matchers.Add(new FragmentsMatcherRegex(
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
        new Regex(@"(?:^|[ \t<{\(\[,:;])(\*(?![\* \t\)])(?:.(?![ \t]\*))*?[^\*\/ \t\n\r\({\[<=\+\-\\@]\*)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        new ClassificationEnum[] { ClassificationEnum.EmphasisMinor }
      ));

      // **bold**
      matchers.Add(new FragmentsMatcherRegex(
        new Regex(@"(?:^|[ \t<{\(\[,:;])(\*\*(?![\* \t\)])(?:.(?![ \t]\*))*?[^\*\/ \t\n\r\({\[<=\+\-\\@]\*\*)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        new ClassificationEnum[] { ClassificationEnum.EmphasisMajor }
      ));

      // _italic_
      matchers.Add(new FragmentsMatcherRegex(
        new Regex(@"(?:^|[ \t<{\(\[,:;])(_(?![_ \t\)])(?:.(?![ \t]_))*?[^_\/ \t\n\r\({\[<=\+\-\\@]_)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        new ClassificationEnum[] { ClassificationEnum.EmphasisMinor }
      ));

      // __bold__
      matchers.Add(new FragmentsMatcherRegex(
        new Regex(@"(?:^|[ \t<{\(\[,:;])(__(?![_ \t\)])(?:.(?![ \t]_))*?[^_\/ \t\n\r\({\[<=\+\-\\@]__)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        new ClassificationEnum[] { ClassificationEnum.EmphasisMajor }
      ));

      // ~~strikethrough~~
      matchers.Add(new FragmentsMatcherRegex(
        new Regex(@"(?:^|[ \t<{\(\[,:;])(~~(?![~ \t\)])(?:.(?![ \t]~))*?[^~\/ \t\n\r\({\[<=\+\-\\@]~~)(?:\r?$|[^a-zA-Z0-9_\*\/~<>])", cOptions, cRegexTimeout),
        new ClassificationEnum[] { ClassificationEnum.Strikethrough }
      ));

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
      string concatKeywords = ConcatKeywordsForRegex(keywords);
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
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      string validFileExtensions = ConcatKeywordsForRegex(cCodeFileExtensions);
      return $@"({cCmdPrefix}{concatKeywords}(?:\{{\.(?:{validFileExtensions})\}})?){cWhitespaceAfterwards}";
    }

    public static string BuildRegex_KeywordAnywhere_WhitespaceAfterwardsRequiredButNoParam(ICollection<string> keywords)
    {
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"({cCmdPrefix}(?:{concatKeywords})){cWhitespaceAfterwards}";
    }

    public static string BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam(ICollection<string> keywords) 
    {
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"({cCmdPrefix}(?:{concatKeywords}))";
    }

    public static string BuildRegex_FormulaEnvironmentStart(ICollection<string> keywords) 
    {
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"({cCmdPrefix}{concatKeywords}\{{.*\}}\{{?)";
    }

    public static string BuildRegex_Language(ICollection<string> keywords) 
    {
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"({cCmdPrefix}{concatKeywords}(?:[^ \t]\w+)?)";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(ICollection<string> keywords)
    {
      string concatKeywords = ConcatKeywordsForRegex(keywords);

      // Example: "\memberof myParameter"
      // NOTE: Although the parameter "myParameter" is required, we nevertheless want to highlight the "\memberof"
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

    public static string BuildRegex_ParamCommand(ICollection<string> keywords)
    {
      string concatKeywords = ConcatKeywordsForRegex(keywords);

      // We need a dedicated parser for the \param command because the "[in,out]" options can have any whitespace
      // before and within the brackets. Moreover, Doxygen requires that the "in" and "out" options may appear
      // at most once; duplicated or unknown options results in Doxygen parsing the \param command incorrectly.
      // Especially note:
      // - This behavior is very different to commands with braces "{...}" such as \snippet:
      //   - Doxygen does not allow whitespace before the "{".
      //   - Doxygen does actually allow duplicated options in braces "{...}".
      //   - Doxygen ignores unknown options in braces "{...}".
      // - This behavior is also different to other commands with brackets "[...]". At the time of writing this,
      //   there are only two other such commands: \htmlonly[block] and \htmlinclude[block]. Apparently,
      //   \htmlonly allows a space before the "[" while \htmlinclude does not. Both do not allow whitespace
      //   within the brackets.
      // => The \param command has very special behavior. Hence we do not parse it the same way as e.g. \snippet,
      //    which uses the FragmentsMatcherForFirstOptionalBracedOptions machinery. Instead, we use a pure regex.
      //
      // https://regex101.com/r/PPNg3R/1
      //
      //                                                                     Optional "[in,out]" parameter                                             name of the function param
      //                                                               ___________________________________________________________________            ________________
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))[ \t]*(\[[ \t]*(?:in|out|in[ \t]*,[ \t]*out|out[ \t]*,[ \t]*in)[ \t]*\])?(?:(?:[ \t]+(\w[^ \t\n\r]*)?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/yCZkWA/1
      string concatKeywords = ConcatKeywordsForRegex(keywords);
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
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))\b(?:[ \t]*([^\n\r]*))?";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/qaaWBO/1
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+([^ \t\n\r]+)?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordAtLineStart_1OptionalBracedParamWithoutSpaceBefore_1RequiredParamAsWord_1OptionalParamTillEnd(
        ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/WuIDFE/1
      string concatKeywords = ConcatKeywordsForRegex(keywords);

      // Example: \snippet{lineno} ....
      // Note: Doxygen does not allow any whitespace before the "{".
      // Note: The part inside the braces "{...}" is parsed in a second step separately.
      //                               Special important parts:  vvvvvvvvvvvvvvvvvvvv                      vv
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))({{[^}}]*?}}(?!}}))?(?:(?:[ \t]+([^ \t\n\r{{]+)?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordAtLineStart_1OptionalBracedParamWithoutSpaceBefore_1RequiredParamTillEnd(
        ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/iumNRV/1
      string concatKeywords = ConcatKeywordsForRegex(keywords);

      // Example: \include{lineno} ....
      // Note: Doxygen does not allow any whitespace before the "{".
      // Note: The part inside the braces "{...}" is parsed in a second step separately.
      //                               Special important parts:  vvvvvvvvvvvvvvvvvvvv                   vv
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))({{[^}}]*?}}(?!}}))?(?:(?:[ \t]+([^\n\r{{]*))?|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/8QcyXW/1
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"{cCommentStart}({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+(""[^\r\n]*?"")?(?:[ \t]+([^\n\r]*))?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd(ICollection<string> keywords)
    {
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/Z7R3xS/1
      string concatKeywords = ConcatKeywordsForRegex(keywords);

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
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"\B({cCmdPrefix}(?:{concatKeywords}))(?:(?:[ \t]+([^ \t\n\r]*)?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWordOrQuoted(ICollection<string> keywords)
    {
      // https://regex101.com/r/yxbTV1/1
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b(?:(?:[ \t]*((?:""[^""]*"")|(?:(?<=[ \t])[^ \t\n\r]*))?)|[\n\r]|$)";
    }

    public static string BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord_1OptionalQuotedParam(ICollection<string> keywords)
    {
      // Examples:
      //   \ref Class::Func()
      //   Text \ref Class.Func() some text
      //   Text \ref subsection1. The point is not part of the parameter.
      //   \ref func(double, int) should match also match the double and int and also the parentheses.
      //   (\ref func()) should not match the final paranthesis (and also of course not the opening one).
      string concatKeywords = ConcatKeywordsForRegex(keywords);

      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      //
      // https://regex101.com/r/EVJaKp/1
      // (1) matches the first parameter to \ref.
      //    First part: Match stuff before potential parentheses
      //       (1a): Match any word character
      //       (1b): But also match "::" and ".". However, we only want to do this if afterwards whitespace comes.
      //             Otherwise, we have an ordinary punctuation character instead of a C++ indirection.
      //             I.e. match the point in "@ref Class.func" but not in "See some @ref class. More text".
      //    Second part (1c): Match optionally available parentheses, including everything between.
      //        To keep things simple, we do not match balanced parentheses; nesting should never happen in this context.
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
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      // Example: \dot "foo test"  width=2\textwidth   height=1cm
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b{cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    public static string BuildRegex_StartUmlCommandWithBracesOptions(ICollection<string> keywords) 
    {
      string concatKeywords = ConcatKeywordsForRegex(keywords);
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
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      // Examples:
      //   Without quotes: @dotfile filename    "foo test" width=200cm height=1cm
      //      With quotes: @dotfile "file name" "foo test" width=200cm height=1cm
      return $@"({cCmdPrefix}(?:{concatKeywords}))\b{cRegexForOptionalFileWithOptionalQuotes}{cRegex_1OptionalCaption_1OptionalSizeIndication}";
    }

    public static string BuildRegex_ImageCommand(ICollection<string> keywords)
    {
      string concatKeywords = ConcatKeywordsForRegex(keywords);
      // Similar to BuildRegex_KeywordAtLineStart_1RequiredParamAsWord(), the required parameter is
      // actually treated as optional (highlight keyword even without parameters while typing).
      // https://regex101.com/r/VN43Fy/1
      return $@"({cCmdPrefix}{concatKeywords}(?:{{.*?}})?)(?:(?:[ \t]+(?:(html|latex|docbook|rtf|xml)\b)?{cRegexForOptionalFileWithOptionalQuotes}{cRegex_1OptionalCaption_1OptionalSizeIndication})|[\n\r]|$)";
    }


    private static string ConcatKeywordsForRegex(ICollection<string> keywords) 
    {
      // We need to order the keywords by **descending** length so that the regex matches the longer keyword first.
      // For example: There are the Doxygen commands "\--" and "\---". So the regex should contain
      // "(---|--)" and not "(--|---)", since the latter will never match "---" while the first does.
      // Also, we need to escape any special characters.
      var orderedAndEscapedKeywords 
        = keywords.OrderByDescending(s => s.Length).ToList().ConvertAll(s => Regex.Escape(s));

      string concatKeywords = string.Join("|", orderedAndEscapedKeywords);
      return concatKeywords;
    }

    /// <summary>
    /// Comparer that sorts formatted fragments by their position in the text. 
    /// Overlapping fragments are treated as equal, so that only one can win in the end.
    /// </summary>
    private class NonOverlappingCommandGroupsComparer : IComparer<FormattedFragmentGroup>
    {
      public int Compare(FormattedFragmentGroup lhs, FormattedFragmentGroup rhs)
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


    private readonly DoxygenCommands mDoxygenCommands;

    private List<IFragmentsMatcher> mMatchers;
    private bool mDisposed = false;

    // In my tests, each individual regex always used less than 100ms.
    // The max. time I was able to measure for a VERY long line was ~60ms.
    private static readonly TimeSpan cRegexTimeout = TimeSpan.FromMilliseconds(100.0);
  }


  //==============================================================================================
  // FragmentsMatcherRegex
  //==============================================================================================

  /// <summary>
  /// A matcher that uses a regex expression to find Doxygen commands, and allows manual rejection
  /// of some fragments via a manual validator delegate.
  /// </summary>
  internal class FragmentsMatcherRegexWithValidator : IFragmentsMatcher
  {
    public delegate bool FragmentValidatorDelegate(int fragmentIndex, string fragmentText);

    public FragmentsMatcherRegexWithValidator(
        Regex re, 
        ClassificationEnum[] classifications, 
        FragmentValidatorDelegate fragmentValidator)
    {
      mRegex = re;
      mClassifications = classifications;
      mFagmentValidator = fragmentValidator;
    }

    public IList<FormattedFragmentGroup> FindFragments(string text)
    {
      var allFragmentGroups = new List<FormattedFragmentGroup>();
      var foundMatches = mRegex.Matches(text);
      foreach (Match m in foundMatches) {
        if (1 < m.Groups.Count && m.Groups.Count <= mClassifications.Length + 1) {
          var fragments = new List<FormattedFragment>();
          for (int idx = 0; idx < m.Groups.Count - 1; ++idx) {
            Group group = m.Groups[idx + 1];
            if (group.Success && group.Captures.Count == 1 && group.Length > 0) {
              if (mFagmentValidator != null && !mFagmentValidator(idx, text.Substring(group.Index, group.Length))) {
                // Once some Doxygen parameter fails to be validated, do not highlight the remaining parameters,
                // even if they were parsed successfully. This makes it more obvious that there is some syntax
                // error in some parameter. Maybe, in the future, we want to change this and instead of stopping
                // we want to continue and show error squiggles under the erroneous parameter.
                break;
              }
              ClassificationEnum classificationsOfGroups = mClassifications[idx];
              fragments.Add(new FormattedFragment(group.Index, group.Length, classificationsOfGroups));
            }
          }

          if (fragments.Count > 0) {
            allFragmentGroups.Add(new FormattedFragmentGroup(fragments));
          }
        }
      }

      return allFragmentGroups;
    }

    private readonly Regex mRegex;
    private readonly ClassificationEnum[] mClassifications;
    private readonly FragmentValidatorDelegate mFagmentValidator;
  }


  /// <summary>
  /// A matcher that uses a pure regex expression (without any "manual" logic) to find Doxygen commands.
  /// </summary>
  internal class FragmentsMatcherRegex : IFragmentsMatcher
  {
    public FragmentsMatcherRegex(Regex re, ClassificationEnum[] classifications)
    {
      mBaseMatcher = new FragmentsMatcherRegexWithValidator(re, classifications, null);
    }

    public IList<FormattedFragmentGroup> FindFragments(string text)
    {
      return mBaseMatcher.FindFragments(text);
    }

    private readonly FragmentsMatcherRegexWithValidator mBaseMatcher;
  }


  /// <summary>
  /// A matcher for Doxygen commands with optional options in braces "{...}" directly after the actual 
  /// Doxygen command. For example:
  /// \snippet{doc} => The "{doc}" is the braced part.
  /// </summary>
  internal class FragmentsMatcherForFirstOptionalBracedOptions : IFragmentsMatcher
  {
    public FragmentsMatcherForFirstOptionalBracedOptions(
        Regex baseRegex, 
        ClassificationEnum[] classifications, 
        Regex[] allowedBracedOptionsRegex)
    {
      mBaseMatcher = new FragmentsMatcherRegexWithValidator(baseRegex, classifications, BracedOptionsValidator);
      mAllowedBracedOptionsRegex = allowedBracedOptionsRegex;
    }

    public IList<FormattedFragmentGroup> FindFragments(string text)
    {
      return mBaseMatcher.FindFragments(text);
    }

    private bool BracedOptionsValidator(int fragmentIndex, string fragmentText) 
    {
      // As the name of the class explains, we look for the braced options at
      // the 1st fragment = 2nd fragment = fragmentIndex 1
      if (fragmentIndex != 1) {
        return true;
      }

      Debug.Assert(fragmentText != null && fragmentText.Length >= 2);

      Debug.Assert(fragmentText.StartsWith("{"));
      Debug.Assert(fragmentText.EndsWith("}"));
      string textWithinBraces = fragmentText.Substring(1, fragmentText.Length - 2);

      // Doxygen always uses a comma to separate the options.
      string[] options = textWithinBraces.Split(',');

      foreach (string option in options) {
        // Doxygen ignores leading and trailing whitespace.
        string trimmedOption = option.Trim();

        // Note: Doxygen ignores empty entries.
        // Note: Doxygen ignores duplicated entries, and thus do we.
        // Note: Doxygen apparently ignores unknown options silently. Nevertheless, if we encounter and
        // unknown option, we stop the highlighting so that the user notices the mistake, especially in
        // case of typos.
        if (trimmedOption.Length > 0 && !mAllowedBracedOptionsRegex.Any(re => re.IsMatch(trimmedOption))) {
          return false;
        }
      }

      return true;
    }

    private readonly FragmentsMatcherRegexWithValidator mBaseMatcher;
    private readonly Regex[] mAllowedBracedOptionsRegex;
  }
}
