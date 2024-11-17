using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace VSDoxyHighlighter
{
  /// <summary>
  /// Represents the help text for a single Doxygen command, which was extracted by an external script
  /// from https://www.doxygen.nl/manual/commands.html
  /// </summary>
  public class DoxygenHelpPageCommand
  {
    public enum OtherTypesEnum
    {
      // Compare the 'Description' property: Used to indicate some piece of text in the help text
      // is some sort of Doxygen command. But we need to figure out how to map it to a specific
      // classification ourselves.
      Command
    }


    /// <summary>
    /// The Doxygen command, without the "\" or "@". For example: "param"
    /// </summary>
    public readonly string Command;

    /// <summary>
    /// The parameters the can be passed to the Doxygen command. E.g., for "param", that would be
    ///    '['dir']' <parameter-name> { parameter description }
    /// </summary>
    public readonly string Parameters;

    /// <summary>
    /// The Hyperlink linking to the online documentation of the command.
    /// </summary>
    public readonly string Hyperlink;

    /// <summary>
    /// The description of the Doxygen command: The string is just the concatenation of the 
    /// individual strings. However, we also have some semantic information:
    /// - If the first item is null, then it is ordinary text.
    /// - If the first item is a ClassificationEnum, then we apply that classification as-is.
    /// - If the first item is a OtherTypesEnum, then we convert it to a ClassificationEnum taking
    ///   into account the user settings.
    /// </summary>
    public readonly (object type, string text, string hyperlink)[] Description;


    public DoxygenHelpPageCommand(string command, string parameters, string hyperlink, (object type, string text, string hyperlink)[] description)
    {
      Command = command;
      Parameters = parameters;
      Hyperlink = hyperlink;
      Description = description;
    }
  }


  /// <summary>
  /// Contains the information for all Doxygen commands that were extracted via a script from the
  /// official help page, but amended with additional information.
  /// </summary>
  public static class AllDoxygenHelpPageCommands 
  {
    /// <summary>
    /// All the Doxygen commands, as extracted from the official help page and amended for use in the extension.
    /// </summary>
    public static readonly List<DoxygenHelpPageCommand> cAmendedDoxygenCommands;


    /// <summary>
    /// Given a specific Doxygen command from the help page, constructs a description suitable for use by Visual Studio
    /// in tool tips.
    /// </summary>
    public static ClassifiedTextElement ConstructDescription(
      CommentParser commentParser, 
      DoxygenHelpPageCommand helpPageCmdInfo,
      bool showHyperlinks)
    {
      string cmdWithSlash = "\\" + helpPageCmdInfo.Command;
      ClassificationEnum commandClassification = commentParser.GetClassificationForCommand(cmdWithSlash);
      return ConstructDescription(commentParser, helpPageCmdInfo, commandClassification, showHyperlinks);
    }


    /// <summary>
    /// The same as the other overload, but expects the classification to be used for the command itself
    /// as parameter instead of computing it on the flow. (In case the classification is already known,
    /// this saves a bit of performance.)
    /// </summary>
    public static ClassifiedTextElement ConstructDescription(
      CommentParser commentParser,
      DoxygenHelpPageCommand helpPageCmdInfo,
      ClassificationEnum commandClassification,
      bool showHyperlinks)
    {
      var runs = new List<ClassifiedTextRun>();

      // Add a line with the actual command.
      string cmdWithSlash = "\\" + helpPageCmdInfo.Command;
      runs.AddRange(ClassifiedTextElement.CreatePlainText("Info for command: ").Runs);
      runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[commandClassification], cmdWithSlash));

      // Add a line with the command's parameters.
      runs.AddRange(ClassifiedTextElement.CreatePlainText("\nCommand parameters: ").Runs);
      if (helpPageCmdInfo.Parameters == "") {
        runs.AddRange(ClassifiedTextElement.CreatePlainText("No parameters").Runs);
      }
      else {
        // Using "Parameter2" since, by default, it is displayed non-bold, causing a nicer display.
        // Note: Attempting to apply the classifications that the user configured for the individual parameters
        // would be nice, but this is hard. The help text cannot be simply parsed with our usual CommentParser,
        // since the help text follows different rules (it uses "[", "<", etc to indicate the semantic of each
        // parameter). So for now we do not attempt this.
        runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[ClassificationEnum.Parameter2], helpPageCmdInfo.Parameters));
      }
      runs.AddRange(ClassifiedTextElement.CreatePlainText("\n\n").Runs);

      // If desired, add a clickable hyperlink to the online documentation.
      if (showHyperlinks && helpPageCmdInfo.Hyperlink != "") {
        runs.AddRange(GetHyperlinkElement("Click HERE to open the online documentation.", helpPageCmdInfo.Hyperlink).Runs);
        runs.AddRange(ClassifiedTextElement.CreatePlainText("\n\n").Runs);
      }

      // Add the whole description.
      if (showHyperlinks) {
        foreach (var descriptionFragment in helpPageCmdInfo.Description) {
          AddTextRunsForDescriptionFragment(commentParser, descriptionFragment, showHyperlinks, runs);
        }
      }
      else {
        int idx = 0;
        while (idx < helpPageCmdInfo.Description.Length) {
          // Without hyperlinks, the "Click here for the corresponding HTML documentation" sentence makes no sense,
          // since the user cannot click anywhere. So don't show it.
          if (idx + 2 < helpPageCmdInfo.Description.Length
              && helpPageCmdInfo.Description[idx].text.Contains("Click") 
              && helpPageCmdInfo.Description[idx + 1].text.Contains("here")
              && helpPageCmdInfo.Description[idx + 2].text.Contains("for the corresponding HTML")) {
            idx += 3;
          }
          else {
            AddTextRunsForDescriptionFragment(commentParser, helpPageCmdInfo.Description[idx], showHyperlinks, runs);
            ++idx;
          }
        }
      }

      return new ClassifiedTextElement(runs);
    }


    private static void AddTextRunsForDescriptionFragment(
      CommentParser commentParser,
      (object type, string text, string hyperlink) descriptionFragment,
      bool showHyperlinks,
      List<ClassifiedTextRun> outputList)
    {
      // Let hyperlinks override every other possible classification, in case we want to show them.
      if (showHyperlinks && descriptionFragment.hyperlink != "") {
        outputList.AddRange(GetHyperlinkElement(descriptionFragment.text, descriptionFragment.hyperlink).Runs);
        return;
      }

      // If we got a very specific classification, use it.
      if (descriptionFragment.type is ClassificationEnum classification) {
        outputList.Add(new ClassifiedTextRun(ClassificationIDs.ToID[classification], descriptionFragment.text));
        return;
      }

      // If we need to figure out the classification by parsing the text, do it now.
      if (descriptionFragment.type is DoxygenHelpPageCommand.OtherTypesEnum otherType) {
        switch (otherType) {
          case DoxygenHelpPageCommand.OtherTypesEnum.Command:
            ClassificationEnum classificationForOther = commentParser.GetClassificationForCommand(descriptionFragment.text);
            outputList.Add(new ClassifiedTextRun(ClassificationIDs.ToID[classificationForOther], descriptionFragment.text));
            return;
          default:
            throw new VSDoxyHighlighterException($"Unknown value for DoxygenHelpPageCommand.OtherTypesEnum: {otherType}");
        }
      }

      // Ordinary plain text.
      outputList.AddRange(ClassifiedTextElement.CreatePlainText(descriptionFragment.text).Runs);
    }


    private static ClassifiedTextElement GetHyperlinkElement(string shownText, string hyperlink) 
    {
      return ClassifiedTextElement.CreateHyperlink(
        text: shownText,
        tooltip: $"Opens \"{hyperlink}\" in your browser.",
        navigationAction: () => {
          // https://stackoverflow.com/a/61035650/3740047
          Process.Start(new ProcessStartInfo(hyperlink) { UseShellExecute = true });
        });
    }


    static AllDoxygenHelpPageCommands()
    {
      // We put some commands that are propably used often to the front of the list that appears in the autocomplete box.
      // The commands will be ordered according to the following list.
      var speciallyOrderedCommands = new List<string>() {
        "param", "tparam", "brief", "details", "note", "warning", "returns", "return",
        "throws", "throw", "sa", "see", "ref", "p", "c", "a", "ingroup",
      };

      cAmendedDoxygenCommands =
        DoxygenCommandsGeneratedFromHelpPage.cCommands.OrderBy(cmd => {
          int idx = speciallyOrderedCommands.IndexOf(cmd.Command);
          return idx != -1 ? idx : speciallyOrderedCommands.Count;
        }).ToList();


      // We additionally modify the list so that various options directly appears in the autocomplete box.
      // Note that when inserting multiple additional variations for one command, they must be listed here
      // in reverse order than how they should appear, since we always insert them directly after the
      // original command.
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "htmlonly", "htmlonly[block]");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{full}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{directory}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{filename}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{extension}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "fileinfo", "fileinfo{file}");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "param", "param[in,out]");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "param", "param[out]");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "param", "param[in]");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "example", "example{lineno}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "dontinclude", "dontinclude{lineno}");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{doc,local}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{lineno,local}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{local}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{doc}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{lineno}");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "htmlinclude", "htmlinclude[block]");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{doc,prefix=YOUR_PREFIX}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{doc,raise=1}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{nostrip}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{strip}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{trimleft,local}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{doc,local}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{lineno,local}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{local}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{trimleft}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{doc}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{lineno}");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{anchor:YOUR_ID}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{inline,anchor:YOUR_ID}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{inline}");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "inheritancegraph", "inheritancegraph{BUILTIN}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "inheritancegraph", "inheritancegraph{GRAPH}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "inheritancegraph", "inheritancegraph{TEXT}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "inheritancegraph", "inheritancegraph{YES}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "inheritancegraph", "inheritancegraph{NO}");

      foreach (string extension in CommentParser.cCodeFileExtensions.Reverse()) {
        InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "code", "code{." + extension + "}");
      }

      // For some reason, the "@{" and "@}" commands are not listed on the Doxygen help page with all commands,
      // but only at https://www.doxygen.nl/manual/grouping.html. Hence, our Python generation script did not
      // automatically add them. So we add them here manually. As a side note, "\{" and "\}" (i.e. the versions with
      // slash instead of @) are not explained in the documentation at all, but they actually work.
      cAmendedDoxygenCommands.Add(new DoxygenHelpPageCommand("{", "", "https://www.doxygen.nl/manual/grouping.html", new (object, string, string)[] { (null, "Opening marker for grouping members of commands such as ", ""), (DoxygenHelpPageCommand.OtherTypesEnum.Command, "\\addtogroup", "https://www.doxygen.nl/manual/commands.html#cmdaddtogroup"), (null, " or ", ""), (DoxygenHelpPageCommand.OtherTypesEnum.Command, "\\defgroup", "https://www.doxygen.nl/manual/commands.html#cmddefgroup"), (null, ".", "") }));
      cAmendedDoxygenCommands.Add(new DoxygenHelpPageCommand("}", "", "https://www.doxygen.nl/manual/grouping.html", new (object, string, string)[] { (null, "Closing marker for grouping members of commands such as ", ""), (DoxygenHelpPageCommand.OtherTypesEnum.Command, "\\addtogroup", "https://www.doxygen.nl/manual/commands.html#cmdaddtogroup"), (null, " or ", ""), (DoxygenHelpPageCommand.OtherTypesEnum.Command, "\\defgroup", "https://www.doxygen.nl/manual/commands.html#cmddefgroup"), (null, ".", "") }));
    }


    private static void InsertCommandVariationAfterOriginal(List<DoxygenHelpPageCommand> commands, string originalCommand, string newCommand)
    {
      int idx = cAmendedDoxygenCommands.FindIndex(x => x.Command == originalCommand);
      if (idx < 0) {
        throw new VSDoxyHighlighterException($"Command '{originalCommand}' not found in list of Doxygen commands.");
      }
      DoxygenHelpPageCommand original = cAmendedDoxygenCommands[idx];
      commands.Insert(idx + 1, new DoxygenHelpPageCommand(newCommand, original.Parameters, original.Hyperlink, original.Description));
    }
  }
}
