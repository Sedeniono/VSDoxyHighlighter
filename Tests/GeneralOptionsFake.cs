using System.Collections.Generic;
using System;


namespace VSDoxyHighlighter.Tests
{

  internal class GeneralOptionsFake : IGeneralOptions
  {
    public bool EnableHighlighting { get; } = true;
    public bool EnableAutocomplete { get; } = true;

    public List<DoxygenCommandInConfig> DoxygenCommandsConfig { get; }

    public bool IsEnabledInCommentType(CommentType type) { return true; }

#pragma warning disable 67
    public event EventHandler SettingsChanged;
#pragma warning restore 67

    public GeneralOptionsFake()
    {
      DoxygenCommandsConfig = DoxygenCommands.DefaultCommandsInConfig;
    }
  }
}
