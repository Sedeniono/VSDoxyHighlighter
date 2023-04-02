using System.Collections.Generic;
using System.Linq;

namespace VSDoxyHighlighter
{
  /// <summary>
  /// Represents a single Doxygen command, which was extracted by an external script
  /// from https://www.doxygen.nl/manual/commands.html
  /// </summary>
  public class DoxygenHelpPageCommand
  {
    public enum OtherTypesEnum
    {
      Command
    }


    /// <summary>
    /// The Doxygen command, without the "\". For example: "param"
    /// </summary>
    public readonly string Command;

    /// <summary>
    /// The parameters the can be passed to the Doxygen command. E.g., for "param", that would be
    ///    '['dir']' <parameter-name> { parameter description }
    /// </summary>
    public readonly string Parameters;

    /// <summary>
    /// The description of the Doxygen command: The string is just the concatenation of the 
    /// individual strings. However, we also have some semantic information:
    /// - If the first item is null, then it is ordinary text.
    /// - If the first item is a ClassificationEnum, then we apply that classification as-is.
    /// - If the first item is a OtherTypesEnum, then we convert it to a ClassificationEnum taking
    ///   into account the user settings.
    /// </summary>
    public readonly (object, string)[] Description;


    public DoxygenHelpPageCommand(string command, string parameters, (object, string)[] description)
    {
      Command = command;
      Parameters = parameters;
      Description = description;
    }
  }


  /// <summary>
  /// Contains the information for all Doxygen commands that were extracted via a script from the
  /// official help page, but amended with additional information.
  /// </summary>
  public static class AllDoxygenHelpPageCommands 
  {
    public static readonly List<DoxygenHelpPageCommand> cAmendedDoxygenCommands;


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
      commands.Insert(idx + 1, new DoxygenHelpPageCommand(newCommand, original.Parameters, original.Description));
    }
  }
}
