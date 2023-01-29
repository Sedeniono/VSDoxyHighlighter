namespace VSDoxyHighlighter
{
  /// <summary>
  /// Represents a single Doxygen command, which was extracted by an external script
  /// from https://www.doxygen.nl/manual/commands.html
  /// </summary>
  public struct DoxygenHelpPageCommand
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
    /// The description according of the Doxygen command.
    /// </summary>
    public readonly string Description;


    public DoxygenHelpPageCommand(string command, string parameters, string description)
    {
      Command = command;
      Parameters = parameters;
      Description = description;
    }
  }
}
