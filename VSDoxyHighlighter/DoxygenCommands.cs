using Microsoft.VisualStudio.TextTemplating;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;


namespace VSDoxyHighlighter
{
  // Types of Doxygen commands that start with "\" or "@". This is used to map different commands to different classifications (i.e. colors).
  // The parameters to the Doxygen commands are not affected by this.
  // Appears in the options dialog.
  public enum DoxygenCommandType
  {
    Command1,
    Note,
    Warning,
  }


  // The parsing results in fragments. For example, "\ref myRef" contains two fragments, "\ref" and "myRef".
  // The first fragment would be the command itself, the second a parameter. This enum is an enumeration of the
  // possible types that the parser finds.
  // Note: This does not necessarily directly map to a specific classification (i.e. color).
  public enum FragmentType
  {
    Command, // The doxygen command itself, e.g. "@param" or "@brief". The command itself can be classified differently, compare DoxygenCommandType.
    Parameter1, // Parameter to some ordinary doxygen command
    Parameter2, // Used for parameters of commands in running text or for some commands with more than one successive parameter
    Title, // Parameter to some ordinary doxygen command that represents a title
    EmphasisMinor, // Usually italic
    EmphasisMajor, // Usually bold
    Strikethrough,
    InlineCode // E.g. `inline code`
  }


  public delegate string RegexCreatorDelegate(ICollection<string> keywords);

  public struct DoxygenCommandGroup
  {
    public List<string> Commands { get; private set; }
    public DoxygenCommandType DoxygenCommandType { get; private set; }

    public RegexCreatorDelegate RegexCreator { get; private set; }

    // For example, "\ref the_reference" contains of two fragments ("\ref" and "the_reference"). These two
    // distinct fragments are parsed by the regex. Their corresponding types are listed here.
    // So this must contain from FragmentType for every group in the regex.
    public ITuple FragmentTypes { get; private set; }

    public DoxygenCommandGroup(
        List<string> cmds,
        DoxygenCommandType doxygenCommandType,
        RegexCreatorDelegate regexCreator,
        ITuple fragmentTypes)
    {
      Commands = cmds;
      DoxygenCommandType = doxygenCommandType;
      RegexCreator = regexCreator;
      FragmentTypes = fragmentTypes;
    }
  }




  public static class DoxygenCommands
  {
    /// <summary>
    /// The default list of commands to use for the options dialog.
    /// </summary>
    public static readonly List<DoxygenCommandInConfig> DefaultDoxygenCommandsInConfig;


    public static List<DoxygenCommandInConfig> ToConfigList(ICollection<DoxygenCommandGroup> commandGroups)
    {
      var result = new List<DoxygenCommandInConfig>();
      foreach (DoxygenCommandGroup cmdGroup in commandGroups) {
        foreach (string cmd in cmdGroup.Commands) {
          var newConfig = new DoxygenCommandInConfig() {
            Command = cmd,
            Classification = cmdGroup.DoxygenCommandType,
            RegexCreator = cmdGroup.RegexCreator,
            FragmentTypes = cmdGroup.FragmentTypes
          };
          result.Add(newConfig);
        }
      }
      return result;
    }


    public static List<DoxygenCommandGroup> FromConfigList(ICollection<DoxygenCommandInConfig> configList)
    {
      var merged = new Dictionary<(DoxygenCommandType, RegexCreatorDelegate, ITuple), List<string>>();
      foreach (DoxygenCommandInConfig config in configList) {
        var arg = (config.Classification, config.RegexCreator, config.FragmentTypes);
        if (!merged.ContainsKey(arg)) {
          merged[arg] = new List<string>();
        }
        merged[arg].Add(config.Command);
      }

      var resultGroups = new List<DoxygenCommandGroup>();
      foreach (var mergedItem in merged) {
        var group = new DoxygenCommandGroup(mergedItem.Value, mergedItem.Key.Item1, mergedItem.Key.Item2, mergedItem.Key.Item3);
        resultGroups.Add(group);
      }

      // Dictionaries are unordered. In principle the order in which we return the results should not matter.
      // For example, it shouldn't matter whether we try to find "\p", "\param" or "\param[in]" first. The
      // employed regex should match only whole words. But just in case I am wrong on this point, we want to
      // be able to reproduce the bug, which is more difficult if it depends on some random order. Moreover,
      // if the user types in an invalid command pattern, for example "\param \ref my_ref", the result might
      // be affected whether first the "\param" or first the "\ref" command gets found.
      // So we sort the result with some arbitrary criterion. Using the negative Count to get the common
      // "\brief" command early one, which is convenient for debugging.
      var sortedResult = resultGroups.OrderBy(
          group => (group.FragmentTypes.Length, -group.Commands.Count, group.FragmentTypes, group.Commands[0])
        ).ToList();

      return sortedResult;
    }


    static DoxygenCommands()
    {
      mDefaultDoxygenCommands = new DoxygenCommandGroup[] {

        //----- With no parameters -------

        new DoxygenCommandGroup(
          new List<string> {
              "brief", "short", "details", "sa", "see", "result", "return", "returns",
              "author", "authors", "copyright", "date", "noop", "else", "endcond", "endif",
              "invariant", "parblock", "endparblock", "post", "pre", "remark", "remarks",
              "since", "test", "version", "callgraph",
              "hidecallgraph", "callergraph", "hidecallergraph", "showrefby", "hiderefby",
              "showrefs", "hiderefs", "endinternal",
              "hideinitializer", "internal", "nosubgrouping", "private",
              "privatesection", "protected", "protectedsection", "public", "publicsection",
              "pure", "showinitializer", "static",
              "addindex", "secreflist", "endsecreflist", "tableofcontents",
              "arg", "li", "docbookonly", "htmlonly", "htmlonly[block]", "latexonly", "manonly",
              "rtfonly", "verbatim", "xmlonly"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_NoParam,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "code"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_CodeCommand,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "fileinfo", "fileinfo{file}", "fileinfo{extension}", "fileinfo{filename}",
            "fileinfo{directory}", "fileinfo{full}",
            "lineinfo", "endlink", "endcode", "enddocbookonly", "enddot", "endmsc",
            "enduml", "endhtmlonly", "endlatexonly", "endmanonly", "endrtfonly",
            "endverbatim", "endxmlonly", "n"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAnywhere_WhitespaceAfterwardsRequiredButNoParam,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            @"f$", @"f(", @"f)", @"f[", @"f]", @"f}",
            @"@", @"&", @"$", @"#", @"<", @">", @"%", @".", @"=", @"::", @"|",
            @"---", @"--", @"{", @"}"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "f"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_FormulaEnvironmentStart,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "~"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_Language,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "warning", "raisewarning"
          },
          DoxygenCommandType.Warning,
          CommentFormatter.BuildRegex_KeywordAtLineStart_NoParam,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "note", "todo", "attention", "bug", "deprecated"
          },
          DoxygenCommandType.Note,
          CommentFormatter.BuildRegex_KeywordAtLineStart_NoParam,
          Tuple.Create(FragmentType.Command)
        ),


        //----- With one parameter -------

        new DoxygenCommandGroup(
          new List<string> {
            "param", "tparam", "param[in]", "param[out]", "param[in,out]", "throw", "throws",
            "exception", "concept", "def", "enum", "extends", "idlexcept", "implements",
            "memberof", "namespace", "package", "relates", "related",
            "relatesalso", "relatedalso", "retval"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord,
          (FragmentType.Command, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "dir", "example", "example{lineno}", "file", "fn", "ingroup", "overload",
            "property", "typedef", "var", "cond",
            "elseif", "if", "ifnot",
            "dontinclude", "dontinclude{lineno}",
            "include", "include{lineno}", "include{doc}", "includelineno", "includedoc",
            "line", "skip", "skipline", "until",
            "verbinclude", "htmlinclude", "htmlinclude[block]", "latexinclude",
            "rtfinclude", "maninclude", "docbookinclude", "xmlinclude"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "cond"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "par", "name"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "mainpage"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd,
          (FragmentType.Command, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "p", "c", "anchor", "cite", "link", "refitem",
            "copydoc", "copybrief", "copydetails", "emoji"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord,
          // Using "Parameter2" to print it non-bold by default, to make the text appearance less disruptive,
          // since these commands are typically place in running text.
          (FragmentType.Command, FragmentType.Parameter2)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "a", "e", "em"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord,
          (FragmentType.Command, FragmentType.EmphasisMinor)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "b"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord,
          (FragmentType.Command, FragmentType.EmphasisMajor)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "qualifier"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWordOrQuoted,
          (FragmentType.Command, FragmentType.Parameter1)
        ),


        //----- With up to two parameters -------

        new DoxygenCommandGroup(
          new List<string> {
            "addtogroup", "defgroup", "headerfile", "page", "weakgroup",
            "section", "subsection", "subsubsection", "paragraph",
            "snippet", "snippet{lineno}", "snippet{doc}", "snippetlineno", "snippetdoc"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "showdate"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "ref", "subpage"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord_1OptionalQuotedParam,
          // Using "Parameter2" to print it non-bold by default, to make the text appearance less disruptive,
          // since these commands are typically place in running text.
          (FragmentType.Command, FragmentType.Parameter2, FragmentType.Title)
        ),

        //----- With up to three parameters -------

        new DoxygenCommandGroup(
          new List<string> {
            "category", "class", "interface", "protocol", "struct", "union"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Parameter2, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "startuml"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_StartUmlCommandWithBracesOptions,
          (FragmentType.Command, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "dot", "msc"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_1OptionalCaption_1OptionalSizeIndication,
          (FragmentType.Command, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1)
        ),

        //----- More parameters -------

        new DoxygenCommandGroup(
          new List<string> {
            "dotfile", "mscfile", "diafile"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "image"
          },
          DoxygenCommandType.Command1,
          CommentFormatter.BuildRegex_ImageCommand,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Parameter2, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1)
        ),

      };


      DefaultDoxygenCommandsInConfig = ToConfigList(mDefaultDoxygenCommands);
    }

    private static readonly DoxygenCommandGroup[] mDefaultDoxygenCommands;
  }


}
