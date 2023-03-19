﻿using EnvDTE;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

namespace VSDoxyHighlighter
{
  //==========================================================================================
  // DoxygenCommandInConfig
  //==========================================================================================

  public enum ParameterTypeInConfig : uint
  {
    Parameter1 = 101,
    Parameter2 = 102,
    Title = 200,
    EmphasisMinor = 300,
    EmphasisMajor = 400,
    Strikethrough = 500,
    InlineCode = 600
  }


  /// <summary>
  /// Represents a single Doxygen command that can be configured by the user.
  /// Note that its members are serialized to and from a string!
  /// </summary>
  [DataContract] // Enables serialization via DoxygenCommandInConfigListSerialization
  public class DoxygenCommandInConfig
  {
    [Category("Command")]
    [DisplayName("Command")]
    [Description("The Doxygen command that gets configured.")]
    [ReadOnly(true)]
    [DataMember(Name = "Cmd", Order = 0)] // Enables serialization via DoxygenCommandInConfigListSerialization
    public string Command { get; set; } = "NEW_COMMAND";

    private const string ClassificationsCategory = "Classifications";

    [Category(ClassificationsCategory)]
    [DisplayName("Command classification")]
    [Description("Specifies which classification from the fonts & colors dialog is used for this command.")]
    [DataMember(Name = "CmdClsif", Order = 1)] // Enables serialization via DoxygenCommandInConfigListSerialization
    public ClassificationEnum CommandClassification { get; set; } = ClassificationEnum.Command1;


    // TODO: Introduce some dummy parameter classifications.
    //       Or rather: Remove the "Command2+3" stuff. Instead, add "Generic1,2,3,4,5..."?

    [Category(ClassificationsCategory)]
    [DisplayName("Parameter classifications")]
    [Description("Allows to change how the parameters of a Doxygen command should get classified.")]
    [DataMember(Name = "Params", Order = 2)] // Enables serialization via DoxygenCommandInConfigListSerialization
    [TypeConverter(typeof(ParameterTypeInConfigArrayConverter))]
    [ReadOnly(true)] // Causes to hide the "..." button (and thus to resize etc the array), but nevertheless allows changing the elements of the array.
    public ClassificationEnum[] ParametersClassifications { get; set; } = new ClassificationEnum[] { };

    // Get a sensible display in the CollectionEditor.
    public override string ToString()
    {
      return Command;
    }
  }


  //==========================================================================================
  // IGeneralOptions
  //==========================================================================================

  /// <summary>
  /// Interface for the properties in the options dialog.
  /// Used to separate the actual properties from the DialogPage, i.e. the Visual Studio GUI stuff.
  /// </summary>
  public interface IGeneralOptions
  {
    bool EnableHighlighting { get; }
    bool EnableAutocomplete { get; }

    List<DoxygenCommandInConfig> DoxygenCommandsConfig { get; }

    event EventHandler SettingsChanged;

    bool IsEnabledInCommentType(CommentType type);
  }



  //==========================================================================================
  // GeneralOptionsPage
  //==========================================================================================

  /// <summary>
  /// Represents the "General" options in the tools -> options menu.
  /// I.e. contains the settings of the extension that can be configured by the user, besides
  /// the classifier ones that Visual Studio automatically puts into the fonts & colors category.
  /// </summary>
  [Guid("c3a8c4bb-8e5a-49a9-b3c3-343ed507f0f9")] // The GUID appears as "Category" in the vssettings file.
  public class GeneralOptionsPage : DialogPage, IGeneralOptions
  {
    // The string that appears in the tools -> options dialog when expanding the main node
    // for the VSDoxyHighlighter entry.
    public const string PageCategory = "General";

    /// <summary>
    /// Called by Visual Studio when the user clicked on the "OK" button in the options dialog.
    /// It is always called, regardless whether an own option or any option at all was changed.
    /// </summary>
    public override void SaveSettingsToStorage()
    {
      base.SaveSettingsToStorage();
      SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Event that gets called when the user clicked on the "OK" button in the options dialog.
    /// It is always called, regardless whether an own option or any option at all was changed.
    /// </summary>
    public event EventHandler SettingsChanged;


    /// <summary>
    /// Returns true if the extension is enabled in comments of the specified <paramref name="type"/>.
    /// </summary>
    public bool IsEnabledInCommentType(CommentType type)
    {
      switch (type) {
        case CommentType.TripleSlash:
          return EnableInTripleSlash;
        case CommentType.DoubleSlashExclamation:
          return EnableInDoubleSlashExclamation;
        case CommentType.DoubleSlash:
          return EnableInDoubleSlash;
        case CommentType.SlashStarStar:
          return EnableInSlashStarStar;
        case CommentType.SlashStarExclamation:
          return EnableInSlashStarExclamation;
        case CommentType.SlashStar:
          return EnableInSlashStar;
        default:
          return false;
      }
    }


    //----------------
    // Flags to enable/disable the main features of the extension

    // "\t" does not get printed, and is a hack to get the category to appear first in the property grid.
    public const string FeaturesSubCategory = "\tFeatures";

    [Category(FeaturesSubCategory)]
    [DisplayName("Enable highlighting")]
    [Description("Enables the syntax highlighting of commands in comments. " 
      + "Note that with the other settings you can define in which comment types the highlighting is enabled.")]
    public bool EnableHighlighting { get; set; } = true;

    [Category(FeaturesSubCategory)]
    [DisplayName("Enable IntelliSense")]
    [Description("Enables the autocomplete of commands while typing in comments (\"IntelliSense\"): "
      + "When enabled and you type a \"\\\" or \"@\" in a comment, a list of all Doxygen commands appears. "
      + "Note that with the other settings you can define in which comment types the autocomplete is enabled.")]
    public bool EnableAutocomplete { get; set; } = true;


    //----------------
    // Comment types

    // "\t" does not get printed, and is a hack to get the category to appear second in the property grid.
    public const string CommentTypesSubCategory = "\tTypes of comments with highlighting and autocomplete";

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"//\"")]
    [Description("Enables the extension in comments starting with \"//\" (but for neither \"///\" nor \"//!\").")]
    public bool EnableInDoubleSlash { get; set; } = false;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"///\"")]
    [Description("Enables the extension in comments starting with \"///\". Note that Visual Studio classifies these "
      + "as \"XML Doc comment\" and might use a different default textSpan color or apply additional highlighting. See the "
      + "\"XML Doc comment\" entries in the \"Fonts and Colors\" settings.")]
    public bool EnableInTripleSlash { get; set; } = true;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"//!\"")]
    [Description("Enables the extension in comments starting with \"//!\".")]
    public bool EnableInDoubleSlashExclamation { get; set; } = true;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"/*\"")]
    [Description("Enables the extension in comments starting with \"/*\" (but for neither \"/**\" nor \"/*!\").")]
    public bool EnableInSlashStar { get; set; } = false;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"/**\"")]
    [Description("Enables the extension in comments starting with \"/**\".")]
    public bool EnableInSlashStarStar { get; set; } = true;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"/*!\"")]
    [Description("Enables the extension in comments starting with \"/*!\".")]
    public bool EnableInSlashStarExclamation { get; set; } = true;


    //----------------
    // Commands configuration

    public const string CommandsConfigurationSubCategory = "Configuration of commands";

    [Category(CommandsConfigurationSubCategory)]
    [DisplayName("Individual Doxygen commands (use the \"...\" button!)")]
    [Description("Allows some configuration of individual Doxygen commands. Please do not edit the string in the grid directly. "
       + "Instead, use the \"...\" button on the right of the row.")]
    // VS cannot serialize the list by itself, need to do it manually. Otherwise, the settings would not get saved.
    [TypeConverter(typeof(DoxygenCommandInConfigListSerialization))]
    [Editor(typeof(FixedElementsCollectionEditor), typeof(System.Drawing.Design.UITypeEditor))]
    public List<DoxygenCommandInConfig> DoxygenCommandsConfig { get; set; } = DoxygenCommands.DefaultCommandsInConfig;
  }



  //==========================================================================================
  // DoxygenCommandInConfigListSerialization
  //==========================================================================================

  /// <summary>
  /// Visual Studio does not support the conversion of lists to and from strings. We need to do it manually.
  /// This class handles the issue for "List<DoxygenCommandInConfig>". As format we use JSON, which is
  /// humanly readable and does not need much escaping when written to XML (the string gets written to
  /// the vssettings file by VS as string, and the vssettings file is XML).
  /// </summary>
  internal class DoxygenCommandInConfigListSerialization : CollectionConverter
  {
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    {
      return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
    {
      return destinationType == typeof(List<DoxygenCommandInConfig>) || base.CanConvertTo(context, destinationType);
    }

    /// <summary>
    /// Conversion **from** string.
    /// </summary>
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      if (value is string valueAsString) {
        using (var memStream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(valueAsString))) {
          try {
            // For development purposes: Uncomment the following line to override the stored settings.
            //return DoxygenCommands.DefaultCommandsInConfig;

            var serializer = new DataContractJsonSerializer(typeof(List<DoxygenCommandInConfig>));
            var commands = (List<DoxygenCommandInConfig>)serializer.ReadObject(memStream);
            ValidateParsedFromString(commands);
            return commands;
          }
          catch (Exception ex) {
            // The serialization can fail if we made some non-backward compatible changes. Or if the user somehow
            // corrupted the JSON string. Or if there is some other bug involved.
            // What should we do in this case? For now, we tell the user about it and restore the default values
            // after creating a backup.
            HandleConversionFromStringError(valueAsString, ex);
            return DoxygenCommands.DefaultCommandsInConfig;
          }
        }
      }
      else {
        return base.ConvertFrom(context, culture, value);
      }
    }


    /// <summary>
    /// Conversion **to** string.
    /// </summary>
    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
    {
      if (destinationType == typeof(string) && value is List<DoxygenCommandInConfig> valueAsT) {
        using (var memStream = new System.IO.MemoryStream()) {
          var serializer = new DataContractJsonSerializer(valueAsT.GetType());
          serializer.WriteObject(memStream, valueAsT);
          return Encoding.UTF8.GetString(memStream.ToArray());
        }
      }
      else {
        return base.ConvertTo(context, culture, value, destinationType);
      }
    }


    private static void ValidateParsedFromString(IEnumerable<DoxygenCommandInConfig> parsed)
    {
      foreach (DoxygenCommandInConfig cmd in parsed) {
        if (!DoxygenCommands.IsKnownDefaultCommand(cmd.Command)) {
          throw new VSDoxyHighlighterException(
            $"Command '{cmd.Command}' is not known.");
        }

        if (!Enum.IsDefined(typeof(ClassificationEnum), cmd.CommandClassification)) {
          throw new VSDoxyHighlighterException(
            $"Command classification converted from string to enum resulted in an invalid enum value '{cmd.CommandClassification}' for command '{cmd.Command}'.");
        }

        for (int paramClsifIdx = 0; paramClsifIdx < cmd.ParametersClassifications.Length; ++paramClsifIdx) {
          ClassificationEnum paramClsif = cmd.ParametersClassifications[paramClsifIdx];
          if (!Enum.IsDefined(typeof(ClassificationEnum), paramClsif)) {
            throw new VSDoxyHighlighterException(
              $"Parameter classification {paramClsifIdx+1} converted from string to enum resulted in an invalid enum value '{paramClsif}' for command '{cmd.Command}'.");
          }
        }
      }
    }


    private static void HandleConversionFromStringError(string valueAsString, Exception ex)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      string backupFilename = Path.GetTempPath() + "VSDoxyHighligher_Commands_" + Guid.NewGuid().ToString() + ".json";
      try {
        using (StreamWriter backupWriter = new StreamWriter(backupFilename)) {
          backupWriter.WriteLine(valueAsString);
        }
      }
      catch (Exception fileEx) {
        ActivityLog.LogError(
          "VSDoxyHighlighter",
          $"Failed to write JSON backup file to '{backupFilename}' during handling of serialization exception. File writing exception: {fileEx}");
        backupFilename = "Failed to write backup file";
      }

      ActivityLog.LogError(
        "VSDoxyHighlighter",
        "Conversion to 'List<DoxygenCommandInConfig>' from string failed. Restoring default configuration for commands."
          + $" Original string was written to file '{backupFilename}'."
          + $"\nException message: {ex}\nString: {valueAsString}");

      InfoBar.ShowMessage(
        icon: KnownMonikers.StatusError,
        message: "VSDoxyHighlighter: Failed to parse Doxygen commands configuration from string. Backed up configuration and restored defaults.",
        actions: new (string, Action)[] {
                  ("Show details",
                    () => MessageBox.Show(
                      "VSDoxyHighlighter extension: Failed to convert the configuration of the Doxygen commands (which is stored as a JSON string) to an actual 'List<DoxygenCommandInConfig>'. "
                      + "Corrupt settings or maybe a bug in the extension?\n"
                      + "Default configuration of commands got restored.\n"
                      + $"Original JSON string written to: {backupFilename}.\n\n"
                      + $"Exception message from the conversion: {ex}\n\n"
                      + $"JSON string that failed to get parse:\n{valueAsString}",
                      "VSDoxyHighlighter error", MessageBoxButtons.OK, MessageBoxIcon.Error))}
      );
    }
  }


  //==========================================================================================
  // ParameterTypeInConfigArrayConverter
  //==========================================================================================

  /// <summary>
  /// Custom converter to define a custom text that should be displayed for the DoxygenCommandInConfig.ParametersClassifications array.
  /// The string is NOT serialized using this converter, because that variable is serialized "manually" via JSON
  /// rather than by the Visual Studio machinery.
  /// </summary>
  internal class ParameterTypeInConfigArrayConverter : ArrayConverter
  {
    public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
    {
      if (destinationType == typeof(string) && value is ParameterTypeInConfig[] valueAsT) {
        if (valueAsT.Length == 0) {
          return "No parameters";
        }
        else if (valueAsT.Length == 1) {
          return "1 parameter";
        }
        else {
          return $"{valueAsT.Length} parameters";
        }
      }
      else {
        return base.ConvertTo(context, culture, value, destinationType);
      }
    }
  }


  //==========================================================================================
  // FixedElementsCollectionEditor
  //==========================================================================================

  /// <summary>
  /// CollectionEditor that is used for the configuration a collection which should support neither
  /// adding nor removing elements or changing the order of the elements.
  /// </summary>
  internal class FixedElementsCollectionEditor : CollectionEditor
  {
    public FixedElementsCollectionEditor(Type type)
       : base(type)
    {
    }

    protected override CollectionForm CreateCollectionForm()
    {
      CollectionForm form = base.CreateCollectionForm();

      try {
        // For now, we do not support adding or removing Doxygen commands. So hide the corresponding buttons.
        ((ButtonBase)form.Controls.Find("addButton", true).First()).Hide();
        ((ButtonBase)form.Controls.Find("removeButton", true).First()).Hide();

        // The order of the Doxygen commands should not be changed by the user, since it shouldn't matter.
        // So hide the corresponding buttons.
        ((ButtonBase)form.Controls.Find("upButton", true).First()).Hide();
        ((ButtonBase)form.Controls.Find("downButton", true).First()).Hide();

        // Hack in the event to force all grid items to be expanded.
        ((PropertyGrid)form.Controls.Find("propertyBrowser", true).First()).SelectedObjectsChanged += OnSelectedObjectsChanged;
      }
      catch (Exception ex) {
        ActivityLog.LogError("VSDoxyHighlighter", $"Exception occurred while creating FixedElementsCollectionEditor: {ex}");
      }

      return form;
    }

    private void OnSelectedObjectsChanged(object sender, EventArgs e)
    {
      ((PropertyGrid)sender).Refresh();
      ((PropertyGrid)sender).ExpandAllGridItems();
    }
  }

}
