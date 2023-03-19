using Microsoft.VisualStudio.Language.Intellisense;
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
  // Note: Appears in the options dialog. Also, the numerical values get serialized.
  public enum DoxygenCommandType : uint
  {
    Command1 = 1,
    Command2 = 20,
    Command3 = 30,
    Note = 100,
    Warning = 200,
    Exceptions = 300,
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


  /// <summary>
  /// Used to group all Doxygen commands that get parsed and classified the same.
  /// The commands of on group are parsed via the same regex (essentially, they get concatenated
  /// via "|" in the regex).
  /// The grouping of commands is important so that commands that result in the same type etc
  /// are parsed with the same regex. So instead of having more than 200 regex, we have less
  /// than 30. That should improve performance.
  /// </summary>
  public struct DoxygenCommandGroup
  {
    public List<string> Commands { get; private set; }
    public DoxygenCommandType DoxygenCommandType { get; private set; }

    public RegexCreatorDelegate RegexCreator { get; private set; }

    // For example, "\ref the_reference" contains of two fragments ("\ref" and "the_reference"). These two
    // distinct fragments are parsed by the regex. Their corresponding types are listed here.
    // So this must contain from FragmentType for every group in the regex.
    public FragmentType[] FragmentTypes { get; private set; }

    public DoxygenCommandGroup(
        List<string> cmds,
        DoxygenCommandType doxygenCommandType,
        RegexCreatorDelegate regexCreator,
        FragmentType[] fragmentTypes)
    {
      Commands = cmds;
      DoxygenCommandType = doxygenCommandType;
      RegexCreator = regexCreator;
      FragmentTypes = fragmentTypes;
    }
  }



  /// <summary>
  /// Represents the "database" of known Doxygen commands, i.e. commands that start with a "\" or "@".
  /// The class itself contains the default settings for the commands as static members.
  /// An instance represents the commands as configured by the user. Note that the only existing
  /// instance should be the one that can be retrieved via `VSDoxyHighlighterPackage.DoxygenCommands`.
  /// </summary>
  public class DoxygenCommands : IDisposable
  {
    public DoxygenCommands(IGeneralOptions options) 
    {
      mGeneralOptions = options; 
      mGeneralOptions.SettingsChanged += OnSettingsChanged;

      InitCommands();
    }

    /// <summary>
    /// Event gets sent when the commands got updated because the user changed the settings.
    /// </summary>
    public event EventHandler CommandsGotUpdated;

    /// <summary>
    /// The default list of commands to use for the options dialog.
    /// </summary>
    public static readonly List<DoxygenCommandInConfig> DefaultCommandsInConfig;

    /// <summary>
    /// The default list of command groups that we define in the extension.
    /// </summary>
    public static readonly DoxygenCommandGroup[] DefaultCommandGroups;

    /// <summary>
    /// Returns all commands, as configured by the user.
    /// </summary>
    public List<DoxygenCommandGroup> CommandGroups { get; private set; }

    /// <summary>
    /// Maps the given command (which must not start with the initial "\" or "@") to the command type,
    /// as configured by the user.
    /// </summary>
    public DoxygenCommandType? FindTypeForCommand(string commandWithoutStart)
    {
      // We need to use a lock since we update the map below, and at least the code for the
      // command autocomplete box calls it from worker threads, while e.g. the format classifier
      // works on the main thread.
      // TODO: Profile.
      lock (mLockForMap) {
        if (mCommandStringToTypeMap.TryGetValue(commandWithoutStart, out var commandType)) {
          return commandType;
        }

        // Some commands such as "\code" come with special regex parsers that attach additional parameters directly to the command.
        // For example, we get as fragmentText "\code{.py}" here. So if we couldn't match it exactly, check for matching start.
        // And also cache the result for the future.
        foreach (DoxygenCommandGroup group in CommandGroups) {
          int commandIdx = group.Commands.FindIndex(origCmd => commandWithoutStart.StartsWith(origCmd));
          if (commandIdx >= 0) {
            mCommandStringToTypeMap.Add(commandWithoutStart, group.DoxygenCommandType); // Cache the result
            return group.DoxygenCommandType;
          }
        }

        return null;
      }
    }


    public void Dispose()
    {
      if (mDisposed) {
        return;
      }
      mDisposed = true;

      if (mGeneralOptions != null) {
        mGeneralOptions.SettingsChanged -= OnSettingsChanged;
      }
    }


    private void OnSettingsChanged(object sender, EventArgs e)
    {
      InitCommands();
      CommandsGotUpdated?.Invoke(this, EventArgs.Empty);
    }


    private void InitCommands()
    {
      CommandGroups = ApplyConfigList(mGeneralOptions.DoxygenCommandsConfig);

      mCommandStringToTypeMap = new Dictionary<string, DoxygenCommandType>();
      foreach (DoxygenCommandGroup group in CommandGroups) {
        foreach (string cmd in group.Commands) {
          mCommandStringToTypeMap.Add(cmd, group.DoxygenCommandType);
        }
      }
    }


    private bool mDisposed = false;
    private readonly IGeneralOptions mGeneralOptions;
    private Dictionary<string /*commandWithoutStart*/, DoxygenCommandType> mCommandStringToTypeMap;
    private static readonly object mLockForMap = new object();

  
    //-----------------------------------------------------------------------------------
    // Static helpers and members

    /// <summary>
    /// Given a collection of commands as configured by the user, returns a copy of the 
    /// default doxygen command groups modified according to the configuration.
    /// </summary>
    private static List<DoxygenCommandGroup> ApplyConfigList(ICollection<DoxygenCommandInConfig> configList)
    {
      var ungrouped = ConfigListToUngroupedGroups(configList);
      return GroupListOfUngrouped(ungrouped);
    }


    /// <summary>
    /// Converts each DoxygenCommandInConfig into exactly one DoxygenCommandGroup. The commands are not grouped.
    /// </summary>
    private static List<DoxygenCommandGroup> ConfigListToUngroupedGroups(ICollection<DoxygenCommandInConfig> configList)
    {
      var ungrouped = new List<DoxygenCommandGroup>();
      foreach (DoxygenCommandInConfig configElem in configList) {
        (int groupIndex, int indexForCommandsInGroup) = FindCommandIndexInDefaults(configElem.Command);
        Debug.Assert(groupIndex >= 0);
        Debug.Assert(indexForCommandsInGroup >= 0);

        if (groupIndex >= 0 && indexForCommandsInGroup >= 0) {
          Debug.Assert(ungrouped.FindIndex(group => group.Commands.Contains(configElem.Command)) < 0);

          DoxygenCommandGroup origGroup = DefaultCommandGroups[groupIndex];

          FragmentType[] fragmentTypes = new FragmentType[configElem.Parameters.Length + 1];
          fragmentTypes[0] = FragmentType.Command;
          for (int idx = 0; idx < configElem.Parameters.Length; ++idx) {
            fragmentTypes[idx + 1] = ParameterTypeToFragmentType(configElem.Parameters[idx]);
          }

          ungrouped.Add(new DoxygenCommandGroup(
            new List<string>() { configElem.Command }, configElem.Classification, origGroup.RegexCreator, fragmentTypes));
        }
      }

      return ungrouped;
    }


    private static (int groupIndex, int indexForCommandsInGroup) FindCommandIndexInDefaults(string cmd) 
    {
      for (int groupIndex = 0; groupIndex < DefaultCommandGroups.Length; ++groupIndex) {
        var group = DefaultCommandGroups[groupIndex];
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
    private static List<DoxygenCommandGroup> GroupListOfUngrouped(ICollection<DoxygenCommandGroup> ungrouped)
    {
      var merged = new Dictionary<
        (string dataAsString, RegexCreatorDelegate), 
        (DoxygenCommandType cmdType, FragmentType[] fragmentTypes, List<string> cmds)>();

      foreach (DoxygenCommandGroup group in ungrouped) {
        // We cannot sensibly use an array as dictionary key, since it won't compare the actual content of the arrays.
        // Workaround: Convert it to a string.
        string dataAsString = string.Concat(group.DoxygenCommandType.ToString(), string.Join("|", group.FragmentTypes));
        var arg = (dataAsString, group.RegexCreator);
        if (!merged.ContainsKey(arg)) {
          merged[arg] = (group.DoxygenCommandType, group.FragmentTypes, new List<string>());
        }

        Debug.Assert(group.Commands.Count == 1);
        Debug.Assert(!merged[arg].cmds.Contains(group.Commands[0]));
        merged[arg].cmds.Add(group.Commands[0]);
      }

      var resultGroups = new List<DoxygenCommandGroup>();
      foreach (var mergedItem in merged) {
        var group = new DoxygenCommandGroup(mergedItem.Value.cmds, mergedItem.Value.cmdType, mergedItem.Key.Item2, mergedItem.Value.fragmentTypes);
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
          group => (group.FragmentTypes.Length, -group.Commands.Count, group.Commands[0])
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
            Classification = cmdGroup.DoxygenCommandType,
            // Skip the first fragment since the user is not supposed to change the fragment type of the command itself.
            Parameters = cmdGroup.FragmentTypes.Skip(1).Select(t => FragmentTypeToParameterType(t)).ToArray()
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


    private static ParameterTypeInConfig FragmentTypeToParameterType(FragmentType type) 
    {
      switch (type) {
        case FragmentType.Parameter1: 
          return ParameterTypeInConfig.Parameter1;
        case FragmentType.Parameter2: 
          return ParameterTypeInConfig.Parameter2;
        case FragmentType.Title: 
          return ParameterTypeInConfig.Title;
        case FragmentType.EmphasisMinor: 
          return ParameterTypeInConfig.EmphasisMinor;
        case FragmentType.EmphasisMajor: 
          return ParameterTypeInConfig.EmphasisMajor;
        case FragmentType.Strikethrough: 
          return ParameterTypeInConfig.Strikethrough;
        case FragmentType.InlineCode: 
          return ParameterTypeInConfig.InlineCode;

        // Especially: FragmentType.Command cannot be converted.
        default:
          Debug.Assert(false);
          throw new VSDoxyHighlighterException($"Attempted to convert FragmentType '{type}' to ParameterTypeInConfig, which is not possible.");
      }
    }


    private static FragmentType ParameterTypeToFragmentType(ParameterTypeInConfig type)
    {
      switch (type) {
        case ParameterTypeInConfig.Parameter1:
          return FragmentType.Parameter1;
        case ParameterTypeInConfig.Parameter2:
          return FragmentType.Parameter2;
        case ParameterTypeInConfig.Title:
          return FragmentType.Title;
        case ParameterTypeInConfig.EmphasisMinor:
          return FragmentType.EmphasisMinor;
        case ParameterTypeInConfig.EmphasisMajor:
          return FragmentType.EmphasisMajor;
        case ParameterTypeInConfig.Strikethrough:
          return FragmentType.Strikethrough;
        case ParameterTypeInConfig.InlineCode:
          return FragmentType.InlineCode;
        default:
          Debug.Assert(false);
          throw new VSDoxyHighlighterException($"Unknown ParameterTypeInConfig '{type}'.");
      }
    }


    static DoxygenCommands()
    {
      DefaultCommandGroups = new DoxygenCommandGroup[] {

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
          new FragmentType[] { FragmentType.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "code"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_CodeCommand,
          new FragmentType[] { FragmentType.Command }
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
          new FragmentType[] { FragmentType.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            @"f$", @"f(", @"f)", @"f[", @"f]", @"f}",
            @"@", @"&", @"$", @"#", @"<", @">", @"%", @".", @"=", @"::", @"|",
            @"---", @"--", @"{", @"}"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam,
          new FragmentType[] { FragmentType.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "f"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_FormulaEnvironmentStart,
          new FragmentType[] { FragmentType.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "~"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_Language,
          new FragmentType[] { FragmentType.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "warning", "raisewarning"
          },
          DoxygenCommandType.Warning,
          CommentParser.BuildRegex_KeywordAtLineStart_NoParam,
          new FragmentType[] { FragmentType.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "note", "todo", "attention", "bug", "deprecated"
          },
          DoxygenCommandType.Note,
          CommentParser.BuildRegex_KeywordAtLineStart_NoParam,
          new FragmentType[] { FragmentType.Command }
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
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "throw", "throws", "exception", "idlexcept"
          },
          DoxygenCommandType.Exceptions,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord,
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1 }
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
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "cond"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd,
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "par", "name"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd,
          new FragmentType[] { FragmentType.Command, FragmentType.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "mainpage"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd,
          new FragmentType[] { FragmentType.Command, FragmentType.Title }
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
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter2 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "a", "e", "em"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord,
          new FragmentType[] { FragmentType.Command, FragmentType.EmphasisMinor }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "b"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord,
          new FragmentType[] { FragmentType.Command, FragmentType.EmphasisMajor }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "qualifier"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWordOrQuoted,
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1 }
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
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1, FragmentType.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "showdate"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd,
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1, FragmentType.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "ref", "subpage"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord_1OptionalQuotedParam,
          // Using "Parameter2" to print it non-bold by default, to make the text appearance less disruptive,
          // since these commands are typically place in running text.
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter2, FragmentType.Title }
        ),

        //----- With up to three parameters -------

        new DoxygenCommandGroup(
          new List<string> {
            "category", "class", "interface", "protocol", "struct", "union"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd,
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1, FragmentType.Parameter2, FragmentType.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "startuml"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_StartUmlCommandWithBracesOptions,
          new FragmentType[] { FragmentType.Command, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "dot", "msc"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_1OptionalCaption_1OptionalSizeIndication,
          new FragmentType[] { FragmentType.Command, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1 }
        ),

        //----- More parameters -------

        new DoxygenCommandGroup(
          new List<string> {
            "dotfile", "mscfile", "diafile"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication,
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "image"
          },
          DoxygenCommandType.Command1,
          CommentParser.BuildRegex_ImageCommand,
          new FragmentType[] { FragmentType.Command, FragmentType.Parameter1, FragmentType.Parameter2, FragmentType.Title, FragmentType.Parameter1, FragmentType.Parameter1 }
        ),

      };


      DefaultCommandsInConfig = ToConfigList(DefaultCommandGroups);
    }
  }


}
