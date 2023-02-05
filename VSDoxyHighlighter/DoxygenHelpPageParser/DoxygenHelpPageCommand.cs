namespace VSDoxyHighlighter
{
  /// <summary>
  /// Represents a single Doxygen command, which was extracted by an external script
  /// from https://www.doxygen.nl/manual/commands.html
  /// </summary>
  public class DoxygenHelpPageCommand
  {
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
    /// individual strings. However, we also have some semantic information. If the FormatType
    /// is null, then it is ordinary text.
    /// </summary>
    public readonly (FormatType?, string)[] Description;


    public DoxygenHelpPageCommand(string command, string parameters, (FormatType?, string)[] description)
    {
      Command = command;
      Parameters = parameters;
      Description = description;
    }
  }
}
