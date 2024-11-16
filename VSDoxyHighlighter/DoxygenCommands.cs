using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;


namespace VSDoxyHighlighter
{
  //==============================================================================================
  // DoxygenCommandMatcherFactory
  //==============================================================================================

  /// <summary>
  /// Abstract factory, that takes a collection of Doxygen commands that are all parsed the same
  /// way and have the same amount of arguments. Each argument is represented by one element in
  /// the given "classifications" array. The result is an object that takes some piece of text
  /// and returns the found Doxygen commands in that text.
  /// </summary>
  public interface IDoxygenCommandsMatcherFactory
  {
    IFragmentsMatcher Create(ICollection<string> commands, ClassificationEnum[] classifications);
  }


  internal delegate string RegexStringGetterDelegate(ICollection<string> commands);


  /// <summary>
  /// Factory that produces a matcher that uses a pure regex expression (without any "manual" logic)
  /// to find Doxygen commands.
  /// </summary>
  internal class DoxygenCommandsMatcherViaRegexFactory : IDoxygenCommandsMatcherFactory
  {
    public DoxygenCommandsMatcherViaRegexFactory(RegexStringGetterDelegate regexStringGetter)
    {
      mRegexStringGetter = regexStringGetter;
    }

    public IFragmentsMatcher Create(ICollection<string> commands, ClassificationEnum[] classifications)
    {
      return new FragmentsMatcherRegex(
        new Regex(mRegexStringGetter(commands), RegexOptions.Compiled | RegexOptions.Multiline),
        classifications
      );
    }

    private readonly RegexStringGetterDelegate mRegexStringGetter;
  }


  /// <summary>
  /// Factory that produces a matcher intended for Doxygen commands with optional clamped first options.
  /// </summary>
  internal class DoxygenCommandsWithOptionalClampedFirstOptionsMatcherFactory : IDoxygenCommandsMatcherFactory
  {
    public DoxygenCommandsWithOptionalClampedFirstOptionsMatcherFactory(
        RegexStringGetterDelegate baseRegexStringGetter,
        string[] allowedClampedOptionsRegex)
    {
      mBaseRegexStringGetter = baseRegexStringGetter;
      mAllowedClampedOptionsRegex = allowedClampedOptionsRegex;
    }

    public IFragmentsMatcher Create(ICollection<string> commands, ClassificationEnum[] classifications)
    {
      Regex[] clampedRegex = mAllowedClampedOptionsRegex.Select(
        regexStr => new Regex($@"\b{regexStr}\b", RegexOptions.Compiled | RegexOptions.Multiline)).ToArray();

      return new FragmentsMatcherForOptionalClampedFirstOptions(
        new Regex(mBaseRegexStringGetter(commands), RegexOptions.Compiled | RegexOptions.Multiline),
        classifications,
        clampedRegex
      );
    }

    private readonly RegexStringGetterDelegate mBaseRegexStringGetter;
    private readonly string[] mAllowedClampedOptionsRegex;
  }


  //==============================================================================================
  // DoxygenCommandGroup
  //==============================================================================================

  /// <summary>
  /// Used to group all Doxygen commands that get parsed and classified the same.
  /// The commands of one group are parsed "in one go" (for example, via the same regex; essentially, 
  /// they get concatenated via "|" in the regex).
  /// The grouping of commands is important so that commands that result in the same type etc
  /// are parsed with the same algorithm. So instead of having more than e.g. 200 regex, we have less
  /// than 30. That should improve performance.
  /// </summary>
  public class DoxygenCommandGroup
  {
    public List<string> Commands { get; private set; }

    public IDoxygenCommandsMatcherFactory MatcherFactory { get; private set; }

    /// <summary>
    /// For each group in the regex, there must be one element. The first element is
    /// for the Doxygen command itself, the remaining ones for the command's parameters.
    /// </summary>
    public ClassificationEnum[] Classifications { get; private set; }

    public DoxygenCommandGroup(
        List<string> cmds,
        IDoxygenCommandsMatcherFactory matcherFactory,
        ClassificationEnum[] classifications)
    {
      Commands = cmds;
      MatcherFactory = matcherFactory;
      Debug.Assert(classifications.Length > 0);
      Classifications = classifications;
    }
  }


  //==============================================================================================
  // DoxygenCommands
  //==============================================================================================

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


    public static bool IsKnownDefaultCommand(string cmd) 
    {
      return DefaultCommandsInConfig.FindIndex(cfgElem => cfgElem.Command == cmd) >= 0;
    }


    /// <summary>
    /// Given the commands as parsed from the Visual Studio configuration file, checks them for errors. If an error
    /// is found, an exception is thrown. Also amends the parsed information in case our extension changed and e.g.
    /// got to know new commands.
    /// 
    /// <param name="configVersion">The version for which the configuration was read. We might want to adapt things to the current version.</param>
    /// </summary>
    public static void ValidateAndAmendCommandsParsedFromConfig(List<DoxygenCommandInConfig> parsed, ConfigVersions configVersion) 
    {
      RemoveObsoleteCommandsFromParsed(parsed, configVersion);

      ValidateParsedFromString(parsed);

      AddNewDefaultCommandsToParsed(parsed, configVersion);
      AdaptParameterClassifications(parsed, configVersion);
      
      SortConfigList(parsed);

      ValidateParameterClassifications(parsed);
    }


    //-----------------------------------------------------------------------------------
    // Private non-static helpers and members

    private void OnSettingsChanged(object sender, EventArgs e)
    {
      InitCommands();
      CommandsGotUpdated?.Invoke(this, EventArgs.Empty);
    }


    private void InitCommands()
    {
      CommandGroups = ApplyConfigList(mGeneralOptions.DoxygenCommandsConfig);
    }


    private bool mDisposed = false;
    private readonly IGeneralOptions mGeneralOptions;

  
    //-----------------------------------------------------------------------------------
    // Private static helpers and members

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

          var classifications = configElem.ParametersClassifications.Prepend(configElem.CommandClassification).ToArray();

          ungrouped.Add(new DoxygenCommandGroup(
            new List<string>() { configElem.Command }, origGroup.MatcherFactory, classifications));
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
        (string dataAsString, IDoxygenCommandsMatcherFactory matcherFactory), 
        (ClassificationEnum[] clsifs, List<string> cmds)>();

      foreach (DoxygenCommandGroup group in ungrouped) {
        // We cannot sensibly use an array as dictionary key, since it won't compare the actual content of the arrays.
        // Workaround: Convert it to a string.
        string dataAsString = string.Join("|", group.Classifications);
        var arg = (dataAsString, group.MatcherFactory);
        if (!merged.ContainsKey(arg)) {
          merged[arg] = (group.Classifications, new List<string>());
        }

        Debug.Assert(group.Commands.Count == 1);
        Debug.Assert(!merged[arg].cmds.Contains(group.Commands[0]));
        merged[arg].cmds.Add(group.Commands[0]);
      }

      var resultGroups = new List<DoxygenCommandGroup>();
      foreach (var mergedItem in merged) {
        var group = new DoxygenCommandGroup(mergedItem.Value.cmds, mergedItem.Key.matcherFactory, mergedItem.Value.clsifs);
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
          group => (group.Classifications.Length, -group.Commands.Count, group.Commands[0])
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
            CommandClassification = cmdGroup.Classifications[0],
            ParametersClassifications = cmdGroup.Classifications.Skip(1).ToArray()
          };
          result.Add(newConfig);
        }
      }

      SortConfigList(result);
      return result;
    }


    /// <summary>
    /// Sorts the commands to make it easier for the user to find a specific command in the options dialog.
    /// We put all the non-letter commands (\~, \<, etc.) at the end, and otherwise sort alphabetically.
    /// </summary>
    private static void SortConfigList(List<DoxygenCommandInConfig> toSort) 
    {
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

      toSort.Sort(CompareConfigs);
    }


    private static void ValidateParsedFromString(IEnumerable<DoxygenCommandInConfig> parsed)
    {
      foreach (DoxygenCommandInConfig cmd in parsed) {
        if (!IsKnownDefaultCommand(cmd.Command)) {
          throw new VSDoxyHighlighterException($"Command '{cmd.Command}' is not known.");
        }

        if (!Enum.IsDefined(typeof(ClassificationEnum), cmd.CommandClassification)) {
          throw new VSDoxyHighlighterException(
            $"Command classification converted from string to enum resulted in an invalid enum value '{cmd.CommandClassification}' for command '{cmd.Command}'.");
        }

        // Note: Length 0 is allowed, but not null.
        if (cmd.ParametersClassifications == null) {
          throw new VSDoxyHighlighterException($"Command '{cmd.Command}' has a null parameter classification.");
        }

        for (int paramClsifIdx = 0; paramClsifIdx < cmd.ParametersClassifications.Length; ++paramClsifIdx) {
          ClassificationEnum paramClsif = cmd.ParametersClassifications[paramClsifIdx];
          if (!Enum.IsDefined(typeof(ClassificationEnum), paramClsif)) {
            throw new VSDoxyHighlighterException(
              $"Parameter classification {paramClsifIdx + 1} converted from string to enum resulted in an invalid enum value '{paramClsif}' for command '{cmd.Command}'.");
          }
        }
      }
    }


    /// <summary>
    /// When we add new Doxygen commands to the extension, we need to amend the data that we read from the
    /// config file with these new commands. This is done by this function.
    /// </summary>
    private static void AddNewDefaultCommandsToParsed(List<DoxygenCommandInConfig> parsed, ConfigVersions configVersion)
    {
      foreach (DoxygenCommandInConfig defaultCmd in DefaultCommandsInConfig) {
        if (!parsed.Any(parsedCmd => parsedCmd.Command == defaultCmd.Command)) {
          parsed.Add(defaultCmd);
        }
      }
    }


    /// <summary>
    /// When we remove known Doxygen commands from the extension, we need to amend the data that we read from the
    /// config file to remove them there, too. This is done by this function.
    /// </summary>
    private static void RemoveObsoleteCommandsFromParsed(List<DoxygenCommandInConfig> parsed, ConfigVersions configVersion)
    {
      if (configVersion < ConfigVersions.v1_8_0) {
        // The `param[...]` and `snippet{...}` commands were removed in version 1.8.0 because a dedicated parser was written
        // to parse them. This leaves just the `param` command itself.
        string[] removedCmds = new[] { 
          "param[in]", "param[out]", "param[in,out]",
          "snippet{lineno}", "snippet{doc}", "snippet{trimleft}", "snippet{local}",
          "snippet{lineno,local}", "snippet{doc,local}", "snippet{trimleft,local}",
          "snippet{local,lineno}", "snippet{local,doc}", "snippet{local,trimleft}"
        };
        foreach (string removedCmd in removedCmds) {
          int idx = parsed.FindIndex(cfgElem => cfgElem.Command == removedCmd);
          if (idx >= 0) {
            parsed.RemoveAt(idx);
          }
        }
      }
    }


    /// <summary>
    /// When we modify a Doxygen command to have more or less parameters in a new version of the VS extension,
    /// we need to amend the data that we read from the config file to mirro this change. This is done by this function.
    /// </summary>
    private static void AdaptParameterClassifications(List<DoxygenCommandInConfig> parsed, ConfigVersions configVersion) 
    {
      if (configVersion < ConfigVersions.v1_8_0) {
        // The `param[...]` and `snippet{...}` commands were removed in version 1.8.0 because a dedicated parser was written to parse them.
        // Thus, the `param` command itself now has two parameters: The `[in,out]` part, and the function parameter name.
        // We need to amend the configuration of `param` to add the default classification for `[in,out]`.
        int idxOfParam = parsed.FindIndex(cfgElem => cfgElem.Command == "param");
        Debug.Assert(idxOfParam >= 0); // Already checked before that the parsed list contains the command.
        DoxygenCommandInConfig parsedParamCmd = parsed[idxOfParam];
        if (parsedParamCmd.ParametersClassifications.Length == 1) {
          parsedParamCmd.ParametersClassifications = new ClassificationEnum[] {
            ClassificationEnum.ParameterClamped, // For `[in,out]`
            parsedParamCmd.ParametersClassifications[0] // Keep previous setting for the function parameter.
          };
        }

        int idxOfSnippet = parsed.FindIndex(cfgElem => cfgElem.Command == "snippet");
        Debug.Assert(idxOfSnippet >= 0); // Already checked before that the parsed list contains the command.
        DoxygenCommandInConfig parsedSnippetCmd = parsed[idxOfSnippet];
        if (parsedSnippetCmd.ParametersClassifications.Length == 2) {
          parsedSnippetCmd.ParametersClassifications = new ClassificationEnum[] {
            ClassificationEnum.ParameterClamped, // For `{...}`
            parsedSnippetCmd.ParametersClassifications[0], // Keep previous setting for the other two parameters
            parsedSnippetCmd.ParametersClassifications[1], // Keep previous setting for the other two parameters
          };
        }
      }
    }


    private static void ValidateParameterClassifications(List<DoxygenCommandInConfig> parsed)
    {
      foreach (DoxygenCommandInConfig parsedCmd in parsed) {
        int defaultCmdIdx = DefaultCommandsInConfig.FindIndex(cfgElem => cfgElem.Command == parsedCmd.Command);
        if (defaultCmdIdx < 0) {
          Debug.Assert(false); // Should not happen because obsolete cmds were removed before.
          continue;
        }
        DoxygenCommandInConfig defaultCmd = DefaultCommandsInConfig[defaultCmdIdx];
        if (parsedCmd.ParametersClassifications.Length != defaultCmd.ParametersClassifications.Length) {
          // Number of parameters from the configuration file is different to the number we expect.
          throw new VSDoxyHighlighterException(
            $"Command '{defaultCmd.Command}' has {parsedCmd.ParametersClassifications.Length} parameters in the configuration file, but expected {defaultCmd.ParametersClassifications.Length}.");
        }
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
              "showrefs", "hiderefs",
              "showinlinesource", "hideinlinesource", "includegraph", "hideincludegraph",
              "includedbygraph", "hideincludedbygraph", "directorygraph", "hidedirectorygraph",
              "collaborationgraph", "hidecollaborationgraph",
              "inheritancegraph", "inheritancegraph{NO}", "inheritancegraph{YES}",
              "inheritancegraph{TEXT}", "inheritancegraph{GRAPH}", "inheritancegraph{BUILTIN}",
              "hideinheritancegraph", "groupgraph", "hidegroupgraph",
              "endinternal", "hideinitializer", "internal", "nosubgrouping", "private",
              "privatesection", "protected", "protectedsection", "public", "publicsection",
              "pure", "showinitializer", "static",
              "addindex", "secreflist", "endsecreflist", "tableofcontents",
              "arg", "li", "docbookonly", "htmlonly", "htmlonly[block]", "latexonly", "manonly",
              "rtfonly", "verbatim", "xmlonly"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_NoParam),
          new ClassificationEnum[] { ClassificationEnum.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "code"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_CodeCommand),
          new ClassificationEnum[] { ClassificationEnum.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "fileinfo", "fileinfo{file}", "fileinfo{extension}", "fileinfo{filename}",
            "fileinfo{directory}", "fileinfo{full}",
            "lineinfo", "endlink", "endcode", "enddocbookonly", "enddot", "endmsc",
            "enduml", "endhtmlonly", "endlatexonly", "endmanonly", "endrtfonly",
            "endverbatim", "endxmlonly", "n"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAnywhere_WhitespaceAfterwardsRequiredButNoParam),
          new ClassificationEnum[] { ClassificationEnum.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "f$", "f(", "f)", "f[", "f]", "f}",
            "\\", "@", "&", "$", "#", "<", ">", "%", "\"", ".", "=", "::", "|",
            "---", "--", "{", "}"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAnywhere_NoWhitespaceAfterwardsRequired_NoParam),
          new ClassificationEnum[] { ClassificationEnum.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "f"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_FormulaEnvironmentStart),
          new ClassificationEnum[] { ClassificationEnum.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "~"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_Language),
          new ClassificationEnum[] { ClassificationEnum.Command }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "warning", "raisewarning"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_NoParam),
          new ClassificationEnum[] { ClassificationEnum.Warning }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "note", "todo", "attention", "bug", "deprecated", "important"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_NoParam),
          new ClassificationEnum[] { ClassificationEnum.Note }
        ),


        //----- With one parameter -------

        new DoxygenCommandGroup(
          new List<string> {
            "tparam",
            "concept", "def", "enum", "extends", "implements",
            "memberof", "module", "namespace", "package", "relates", "related",
            "relatesalso", "relatedalso", "retval"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "throw", "throws", "exception", "idlexcept"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord),
          new ClassificationEnum[] { ClassificationEnum.Exceptions, ClassificationEnum.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "dir", "example", "example{lineno}", "file", "fn", "ingroup", "overload",
            "property", "typedef", "var",
            "elseif", "if", "ifnot",
            "dontinclude", "dontinclude{lineno}",
            "include", "include{lineno}", "include{doc}", "include{local}",
            "include{lineno,local}", "include{doc,local}", "include{local,lineno}", "include{local,doc}",
            "includelineno", "includedoc",
            "line", "skip", "skipline", "until",
            "verbinclude", "htmlinclude", "htmlinclude[block]", "latexinclude",
            "rtfinclude", "maninclude", "docbookinclude", "xmlinclude"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "cond"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "par", "name"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1OptionalParamTillEnd),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "mainpage"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamTillEnd),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "doxyconfig"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "p", "c", "anchor", "cite", "link", "refitem",
            "copydoc", "copybrief", "copydetails", "emoji"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord),
          // Using "Parameter2" to print it non-bold by default, to make the text appearance less disruptive,
          // since these commands are typically place in running text.
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter2 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "a", "e", "em"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.EmphasisMinor }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "b"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.EmphasisMajor }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "qualifier"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWordOrQuoted),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1 }
        ),


        //----- With up to two parameters -------
        new DoxygenCommandGroup(
          new List<string> {
            "param"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_ParamCommand),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.ParameterClamped, ClassificationEnum.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "addtogroup", "defgroup", "headerfile", "page", "weakgroup",
            "section", "subsection", "subsubsection", "paragraph", "subparagraph", "subsubparagraph",
            "snippetlineno", "snippetdoc"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamTillEnd),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1, ClassificationEnum.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "showdate"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1RequiredQuotedParam_1OptionalParamTillEnd),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1, ClassificationEnum.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "ref", "subpage"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordSomewhereInLine_1RequiredParamAsWord_1OptionalQuotedParam),
          // Using "Parameter2" to print it non-bold by default, to make the text appearance less disruptive,
          // since these commands are typically place in running text.
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter2, ClassificationEnum.Title }
        ),

        //----- With up to three parameters -------

        new DoxygenCommandGroup(
          new List<string> {
            "snippet"
          },
          new DoxygenCommandsWithOptionalClampedFirstOptionsMatcherFactory(
            baseRegexStringGetter: CommentParser.BuildRegex_KeywordAtLineStart_1OptionalBracedParam_1RequiredParamAsWord_1OptionalParamTillEnd,
            allowedClampedOptionsRegex: new string[] { "lineno", "trimleft", "doc", "local", "strip", "nostrip", @"raise=\d", @"prefix=\w+" }
          ),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.ParameterClamped, ClassificationEnum.Parameter1, ClassificationEnum.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "category", "class", "interface", "protocol", "struct", "union"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_KeywordAtLineStart_1RequiredParamAsWord_1OptionalParamAsWord_1OptionalParamTillEnd),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1, ClassificationEnum.Parameter2, ClassificationEnum.Title }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "startuml"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_StartUmlCommandWithBracesOptions),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Title, ClassificationEnum.Parameter1, ClassificationEnum.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "dot", "msc"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_1OptionalCaption_1OptionalSizeIndication),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Title, ClassificationEnum.Parameter1, ClassificationEnum.Parameter1 }
        ),

        //----- More parameters -------

        new DoxygenCommandGroup(
          new List<string> {
            "dotfile", "mscfile", "diafile"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_1File_1OptionalCaption_1OptionalSizeIndication),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1, ClassificationEnum.Title, ClassificationEnum.Parameter1, ClassificationEnum.Parameter1 }
        ),

        new DoxygenCommandGroup(
          new List<string> {
            "image"
          },
          new DoxygenCommandsMatcherViaRegexFactory(CommentParser.BuildRegex_ImageCommand),
          new ClassificationEnum[] { ClassificationEnum.Command, ClassificationEnum.Parameter1, ClassificationEnum.Parameter2, ClassificationEnum.Title, ClassificationEnum.Parameter1, ClassificationEnum.Parameter1 }
        ),

      };


      DefaultCommandsInConfig = ToConfigList(DefaultCommandGroups);
    }
  }
}
