using Microsoft.VisualStudio.TextTemplating;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;


namespace VSDoxyHighlighter
{
  // Types of Doxygen commands that start with "\" or "@". This is used to map different commands to different classifications (i.e. colors).
  // The parameters to the Doxygen commands are not affected by this.
  // Appears in the options dialog. The numerical values get serialized.
  public enum DoxygenCommandType : uint
  {
    Command1 = 1,
    Command2 = 2,
    Command3 = 3,
    Note = 10,
    Warning = 20,
    Exceptions = 30,
  }


  // The parsing results in fragments. For example, "\ref myRef" contains two fragments, "\ref" and "myRef".
  // The first fragment would be the command itself, the second a parameter. This enum is an enumeration of the
  // possible types that the parser finds.
  // Note: This does not necessarily directly map to a specific classification (i.e. color).
  public enum FragmentType
  {
    Command, // The doxygen command itself, e.g. "@param" or "@brief". Different commands are mapped to different classifications, compare DoxygenCommandType.
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


    /// <summary>
    /// Given a collection of commands as configured by the user, returns a copy of the 
    /// default doxygen command groups modified according to the configuration.
    /// </summary>
    public static List<DoxygenCommandGroup> ApplyConfigList(ICollection<DoxygenCommandInConfig> configList) 
    {
      var ungrouped = new List<DoxygenCommandGroup>();
      foreach (DoxygenCommandInConfig configElem in configList) {
        (int groupIndex, int indexForCommandsInGroup) = FindCommandIndexInDefaults(configElem.Command);
        Debug.Assert(groupIndex >= 0);
        Debug.Assert(indexForCommandsInGroup >= 0);

        if (groupIndex >= 0 && indexForCommandsInGroup >= 0) {
          Debug.Assert(ungrouped.FindIndex(group => group.Commands.Contains(configElem.Command)) < 0);

          DoxygenCommandGroup origGroup = mDefaultDoxygenCommands[groupIndex];
          ungrouped.Add(new DoxygenCommandGroup(
            new List<string>() { configElem.Command }, configElem.Classification, origGroup.RegexCreator, origGroup.FragmentTypes));
        }
      }

      return FromUngroupedList(ungrouped);
    }


    private static (int groupIndex, int indexForCommandsInGroup) FindCommandIndexInDefaults(string cmd) 
    {
      for (int groupIndex = 0; groupIndex < mDefaultDoxygenCommands.Length; ++groupIndex) {
        var group = mDefaultDoxygenCommands[groupIndex];
        int cmdIndex = group.Commands.FindIndex(s => s == cmd);
        if (cmdIndex >= 0) { 
          return (groupIndex, cmdIndex);
        }
      }
      return (-1, -1);
    }


    /// <summary>
    /// Given a collection of groups with only a single command in each group, groups all of them together.
    /// </summary>
    private static List<DoxygenCommandGroup> FromUngroupedList(ICollection<DoxygenCommandGroup> ungrouped)
    {
      var merged = new Dictionary<(DoxygenCommandType, RegexCreatorDelegate, ITuple), List<string>>();
      foreach (DoxygenCommandGroup group in ungrouped) {
        var arg = (group.DoxygenCommandType, group.RegexCreator, group.FragmentTypes);
        if (!merged.ContainsKey(arg)) {
          merged[arg] = new List<string>();
        }

        Debug.Assert(group.Commands.Count == 1);
        Debug.Assert(!merged[arg].Contains(group.Commands[0]));
        merged[arg].Add(group.Commands[0]);
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

    /// <summary>
    /// Returns the appropriate list to be used in the options dialog to configure the given command groups.
    /// </summary>
    private static List<DoxygenCommandInConfig> ToConfigList(ICollection<DoxygenCommandGroup> commandGroups)
    {
      var result = new List<DoxygenCommandInConfig>();
      foreach (DoxygenCommandGroup cmdGroup in commandGroups) {
        foreach (string cmd in cmdGroup.Commands) {
          var newConfig = new DoxygenCommandInConfig() {
            Command = cmd,
            Classification = cmdGroup.DoxygenCommandType
          };
          result.Add(newConfig);
        }
      }

      // Sort the commands to make it easier for the user to find a specific command in the options dialog.
      // We put all the non-letter commands (\~, \<, etc.) at the end, and otherwise sort alphabetically.
      int CompareConfigs(DoxygenCommandInConfig c1, DoxygenCommandInConfig c2) 
      {
        bool isLetter1 = char.IsLetter(c1.Command[0]);
        bool isLetter2 = char.IsLetter(c2.Command[0]);
        if ((isLetter1 && isLetter2) || (!isLetter1 && !isLetter2)) {
          return string.Compare(c1.Command, c2.Command, StringComparison.InvariantCulture);
        }
        else if (isLetter1) {
          return -1;
        }
        else {
          return 1;
        }
      }

      result.Sort(CompareConfigs);
      return result;
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
          CommentParser.BuildRegex_KeywordAtLineStart_NoParam,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "code"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_CodeCommand,
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
          CommentParser.BuildRegex_KeywordAnywhere_WhitespaceAfterwardsRequiredButNoParam,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            @"f$", @"f(", @"f)", @"f[", @"f]", @"f}",
            @"@", @"&", @"$", @"#", @"<", @">", @"%", @".", @"=", @"::", @"|",
            @"---", @"--", @"{", @"}"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "f"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_FormulaEnvironmentStart,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "~"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_Language,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "warning", "raisewarning"
          },
          DoxygenCommandType.Warning,
          CommentParser.BuildRegex_KeywordAtLineStart_NoParam,
          Tuple.Create(FragmentType.Command)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "note", "todo", "attention", "bug", "deprecated"
          },
          DoxygenCommandType.Note,
          CommentParser.BuildRegex_KeywordAtLineStart_NoParam,
          Tuple.Create(FragmentType.Command)
        ),


        //----- With one parameter -------

        new DoxygenCommandGroup(
          new List<string> {
            "param", "tparam", "param[in]", "param[out]", "param[in,out]",
            "concept", "def", "enum", "extends", "implements",
            "memberof", "namespace", "package", "relates", "related",
            "relatesalso", "relatedalso", "retval"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord,
          (FragmentType.Command, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "throw", "throws", "exception", "idlexcept"
          },
          DoxygenCommandType.Exceptions,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord,
          (FragmentType.Command, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "dir", "example", "example{lineno}", "file", "fn", "ingroup", "overload",
            "property", "typedef", "var",
            "elseif", "if", "ifnot",
            "dontinclude", "dontinclude{lineno}",
            "include", "include{lineno}", "include{doc}", "includelineno", "includedoc",
            "line", "skip", "skipline", "until",
            "verbinclude", "htmlinclude", "htmlinclude[block]", "latexinclude",
            "rtfinclude", "maninclude", "docbookinclude", "xmlinclude"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "cond"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "par", "name"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "mainpage"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd,
          (FragmentType.Command, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "p", "c", "anchor", "cite", "link", "refitem",
            "copydoc", "copybrief", "copydetails", "emoji"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord,
          // Using "Parameter2" to print it non-bold by default, to make the text appearance less disruptive,
          // since these commands are typically place in running text.
          (FragmentType.Command, FragmentType.Parameter2)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "a", "e", "em"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord,
          (FragmentType.Command, FragmentType.EmphasisMinor)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "b"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord,
          (FragmentType.Command, FragmentType.EmphasisMajor)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "qualifier"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWordOrQuoted,
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
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "showdate"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "ref", "subpage"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord_1OptionalQuotedParam,
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
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Parameter2, FragmentType.Title)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "startuml"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_StartUmlCommandWithBracesOptions,
          (FragmentType.Command, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "dot", "msc"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_1OptionalCaption_1OptionalSizeIndication,
          (FragmentType.Command, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1)
        ),

        //----- More parameters -------

        new DoxygenCommandGroup(
          new List<string> {
            "dotfile", "mscfile", "diafile"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1)
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "image"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_ImageCommand,
          (FragmentType.Command, FragmentType.Parameter1, FragmentType.Parameter2, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1)
        ),

      };


      DefaultDoxygenCommandsInConfig = ToConfigList(mDefaultDoxygenCommands);
    }

    private static readonly DoxygenCommandGroup[] mDefaultDoxygenCommands;
  }


}
