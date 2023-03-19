﻿namespace VSDoxyHighlighter
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
}
