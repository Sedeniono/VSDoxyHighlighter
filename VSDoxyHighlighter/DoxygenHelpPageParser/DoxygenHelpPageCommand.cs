﻿using Microsoft.VisualStudio.Text.Adornments;
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
    /// The html anchor for the command. I.e. appending this anchor "https://www.doxygen.nl/manual/commands.html#"
    /// results in a hyperlink to the online documentation.
    /// </summary>
    public readonly string Anchor;

    /// <summary>
    /// The description of the Doxygen command: The string is just the concatenation of the 
    /// individual strings. However, we also have some semantic information:
    /// - If the first item is null, then it is ordinary text.
    /// - If the first item is a ClassificationEnum, then we apply that classification as-is.
    /// - If the first item is a OtherTypesEnum, then we convert it to a ClassificationEnum taking
    ///   into account the user settings.
    /// </summary>
    public readonly (object, string)[] Description;


    public DoxygenHelpPageCommand(string command, string parameters, string anchor, (object, string)[] description)
    {
      Command = command;
      Parameters = parameters;
      Anchor = anchor;
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

    private static readonly string cOnlineDocumentationLink = "https://www.doxygen.nl/manual/commands.html";


    /// <summary>
    /// Given a specific Doxygen command from the help page, constructs a description suitable for use by Visual Studio
    /// in tool tips.
    /// </summary>
    public static ClassifiedTextElement ConstructDescription(
      CommentParser commentParser, 
      DoxygenHelpPageCommand helpPageInfo,
      bool showHyperlinks)
    {
      string cmdWithSlash = "\\" + helpPageInfo.Command;
      ClassificationEnum commandClassification = commentParser.GetClassificationForCommand(cmdWithSlash);
      return ConstructDescription(commentParser, helpPageInfo, commandClassification, showHyperlinks);
    }


    /// <summary>
    /// The same as the other overload, but expects the classification to be used for the command itself
    /// as parameter instead of computing it on the flow. (In case the classification is already known,
    /// this saves a bit of performance.)
    /// </summary>
    public static ClassifiedTextElement ConstructDescription(
      CommentParser commentParser,
      DoxygenHelpPageCommand helpPageInfo,
      ClassificationEnum commandClassification,
      bool showHyperlinks)
    {
      var runs = new List<ClassifiedTextRun>();

      // Add a line with the actual command.
      string cmdWithSlash = "\\" + helpPageInfo.Command;
      runs.AddRange(ClassifiedTextElement.CreatePlainText("Info for command: ").Runs);
      runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[commandClassification], cmdWithSlash));

      // Add a line with the command's parameters.
      runs.AddRange(ClassifiedTextElement.CreatePlainText("\nCommand parameters: ").Runs);
      if (helpPageInfo.Parameters == "") {
        runs.AddRange(ClassifiedTextElement.CreatePlainText("No parameters").Runs);
      }
      else {
        // Using "Parameter2" since, by default, it is displayed non-bold, causing a nicer display.
        // Note: Attempting to apply the classifications that the user configured for the individual parameters
        // would be nice, but this is hard. The help text cannot be simply parsed with our usual CommentParser,
        // since the help text follows different rules (it uses "[", "<", etc to indicate the semantic of each
        // parameter). So for now we do not attempt this.
        runs.Add(new ClassifiedTextRun(ClassificationIDs.ToID[ClassificationEnum.Parameter2], helpPageInfo.Parameters));
      }
      runs.AddRange(ClassifiedTextElement.CreatePlainText("\n\n").Runs);

      // If desired, add a clickable hyperlink to the only documentation.
      if (showHyperlinks) {
        string hyperlink = $"{cOnlineDocumentationLink}#{helpPageInfo.Anchor}";
        runs.AddRange(ClassifiedTextElement.CreateHyperlink(
            text: "Click HERE to open the online documentation.",
            tooltip: $"Opens \"{hyperlink}\" in your browser.",
            navigationAction: () => {
              // https://stackoverflow.com/a/61035650/3740047
              Process.Start(new ProcessStartInfo(hyperlink) { UseShellExecute = true });
            })
          .Runs);
        runs.AddRange(ClassifiedTextElement.CreatePlainText("\n\n").Runs);
      }

      // Add the whole description.
      foreach (var descriptionFragment in helpPageInfo.Description) {
        AddTextRunsForDescriptionFragment(commentParser, descriptionFragment, runs);
      }

      return new ClassifiedTextElement(runs);
    }


    private static void AddTextRunsForDescriptionFragment(
      CommentParser commentParser,
      (object, string) descriptionFragment, // Compare DoxygenHelpPageCommand
      List<ClassifiedTextRun> outputList)
    {
      if (descriptionFragment.Item1 is ClassificationEnum classification) {
        // Use the given classification as-is.
        outputList.Add(new ClassifiedTextRun(ClassificationIDs.ToID[classification], descriptionFragment.Item2));
        return;
      }

      if (descriptionFragment.Item1 is DoxygenHelpPageCommand.OtherTypesEnum otherType) {
        switch (otherType) {
          case DoxygenHelpPageCommand.OtherTypesEnum.Command:
            ClassificationEnum classificationForOther = commentParser.GetClassificationForCommand(descriptionFragment.Item2);
            outputList.Add(new ClassifiedTextRun(ClassificationIDs.ToID[classificationForOther], descriptionFragment.Item2));
            return;
          default:
            throw new VSDoxyHighlighterException($"Unknown value for DoxygenHelpPageCommand.OtherTypesEnum: {otherType}");
        }
      }

      outputList.AddRange(ClassifiedTextElement.CreatePlainText(descriptionFragment.Item2).Runs);
    }


    static AllDoxygenHelpPageCommands()
    {
      // We put some commands that are propably used often to the front of the list that appears in the autocomplete box.
      // The commands will be ordered according to the following list.
      var speciallyOrderedCommands = new List<string>() {
        "brief", "details", "note", "warning", "param", "tparam", "returns", "return",
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
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "htmlonly", "[block]");

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
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{lineno}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "include", "include{doc}");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "htmlinclude", "htmlinclude[block]");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{doc}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "snippet", "snippet{lineno}");

      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{anchor:YOUR_ID}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{inline,anchor:YOUR_ID}");
      InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "image", "image{inline}");

      foreach (string extension in CommentParser.cCodeFileExtensions.Reverse()) {
        InsertCommandVariationAfterOriginal(cAmendedDoxygenCommands, "code", "code{." + extension + "}");
      }
    }


    private static void InsertCommandVariationAfterOriginal(List<DoxygenHelpPageCommand> commands, string originalCommand, string newCommand)
    {
      int idx = cAmendedDoxygenCommands.FindIndex(x => x.Command == originalCommand);
      if (idx < 0) {
        throw new VSDoxyHighlighterException($"Command '{originalCommand}' not found in list of Doxygen commands.");
      }
      DoxygenHelpPageCommand original = cAmendedDoxygenCommands[idx];
      commands.Insert(idx + 1, new DoxygenHelpPageCommand(newCommand, original.Parameters, original.Anchor, original.Description));
    }
  }
}
