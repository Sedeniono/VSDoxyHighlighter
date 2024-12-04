using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
    [Category("\tCommand")] // "\t" does not get printed, and is a hack to get the category to appear first in the property grid.
    [DisplayName("Command")]
    [Description("The Doxygen command that gets configured.")]
    [ReadOnly(true)]
    [DataMember(Name = "Cmd", Order = 0, IsRequired = true)] // Enables serialization via DoxygenCommandInConfigListSerialization
    public string Command { get; set; } = "NEW_COMMAND";

    private const string ClassificationsCategory = "Classifications";

    [Category(ClassificationsCategory)]
    [DisplayName("Command classification")]
    [Description("Specifies which classification from the fonts & colors dialog is used for this command.")]
    [DataMember(Name = "CmdCls", Order = 1, IsRequired = true)] // Enables serialization via DoxygenCommandInConfigListSerialization
    public ClassificationEnum CommandClassification { get; set; } = ClassificationEnum.Command;

    [Category(ClassificationsCategory)]
    [DisplayName("Parameter classifications")]
    [Description("Allows to change how the parameters of a Doxygen command should get classified.")]
    [DataMember(Name = "ParCls", Order = 2, IsRequired = true)] // Enables serialization via DoxygenCommandInConfigListSerialization
    [TypeConverter(typeof(ParameterTypeInConfigArrayConverter))] // Just changes the default value displayed in the property grid. Not used by the serialization.
    [ReadOnly(true)] // Causes to hide the "..." button (and thus to resize etc the array), but nevertheless allows changing the elements of the array.
    public ClassificationEnum[] ParametersClassifications { get; set; } = new ClassificationEnum[] { };

    // Get a sensible display in the CollectionEditor.
    public override string ToString()
    {
      return Command;
    }
  }


  //==========================================================================================
  // ConfigVersions
  //==========================================================================================

  // In some versions of VSDoxyHighlighter, we need to change the configuration format. Each time this happens,
  // we increment the version written to the config file. This allow to react appropriately when reading old
  // config files.
  // Note: We decouple the VSDoxyHighlighter version from the config version, because we do not necessarily
  // change the config format in each version such that it requires special code to read old configs.
  public enum ConfigVersions
  {
    NoVersionInConfig = 0,
    v1_7_0 = 1000, // Versions <=1.7.0
    v1_8_0 = 2000, // Config format change in 1.8.0
    Current = v1_8_0,
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
    // Note: Stored as integer rather than as `ConfigVersions` because we don't want the enum label to
    // be used in the XML when the configuration gets exported by VS, but rather the numeric value. This
    // is especially important because not all enum labels have a unique numeric value.
    int Version { get; }

    bool EnableHighlighting { get; }
    bool EnableAutocomplete { get; }
    bool EnableQuickInfo { get; }

    bool EnableParameterAutocompleteFor_param { get; }
    bool EnableParameterAutocompleteFor_tparam { get; }
    bool EnableParameterAutocompleteFor_p_a { get; }

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

    
    public override void LoadSettingsFromStorage()
    {
      // Visual Studio can trigger loading the settings multiple times (e.g. when clicking the cancel button
      // in the Visual Studio options page). Old configurations before 1.8.0 did not store the "Version" (because
      // it didn't exist at that time). If we didn't reset the "Version" here manually, it would mean that "Version"
      // retains the value from a previous call to LoadSettingsFromStorage() since it doesn't get overwritten
      // because it doesn't exist in the config. Hence, after LoadSettingsFromStorage() below we would end up with
      // an inconsistent state: The "Version" already is set to "Current", while the actual data (Doxygen commands etc.)
      // is again for the old version. => Reset "Version" here manually so that we adapt the old config again correctly.
      Version = (int)ConfigVersions.NoVersionInConfig;

      base.LoadSettingsFromStorage();

      if (Version == (int)ConfigVersions.NoVersionInConfig) {
        // We come here in two cases:
        // 1) The settings contain no VsDoxyHighlighter configuration at all yet
        //    => The properties contain the default values => They are for the current VSDoxyHighlighter version.
        // 2) We have a config for version <1.8.0, which did not contain the "Version" property yet. To detect this
        //    case, we use a hack: If "param[in]" is in the config, it must be a version <1.8.0. Which version doesn't
        //    really matter for the code that checks the version as long as it is <1.8.0, so simply use 1.7.0.
        if (DoxygenCommandsConfig.FindIndex(cmd => cmd.Command == "param[in]") >= 0) {
          // Case (2)
          Version = (int)ConfigVersions.v1_7_0;
        }
        else {
          // Case (1)
          Version = (int)ConfigVersions.Current;
        }
      }

      // Validate the read configuration. And if we read the configuration of an old version of the extension, we
      // might have changed the available configuration since then. Make appropriate amendments to port the old config
      // to the new one.
      DoxygenCommands.ValidateAndAmendCommandsParsedFromConfig(DoxygenCommandsConfig, (ConfigVersions)Version);

      // Since we adapted the configuration, it now is in the latest format. So ensure that the current version number
      // gets written the next time the config is saved.
      Version = (int)ConfigVersions.Current;
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

    // The value is not editable by the user.
    [Browsable(false)]
    public int Version { get; set; } = (int)ConfigVersions.NoVersionInConfig;


    //----------------
    // Flags to enable/disable the main features of the extension

    // "\t" does not get printed, and is a hack to get the category to appear first in the property grid.
    public const string FeaturesSubCategory = "\t\tMajor features (detailed configuration in later sections)";

    [Category(FeaturesSubCategory)]
    [DisplayName("Enable highlighting")]
    [Description("Enables the syntax highlighting of commands in comments. " 
      + "Note that with the settings below you can define in which comment types the highlighting is enabled.")]
    public bool EnableHighlighting { get; set; } = true;

    [Category(FeaturesSubCategory)]
    [DisplayName("Enable IntelliSense")]
    [Description("Enables the autocomplete of commands while typing in comments (\"IntelliSense\"): "
      + "When enabled and you type a \"\\\" or \"@\" in a comment, a list of all Doxygen commands appears. "
      + "Some specific commands support autocompletion for their arguments (see corresponding detailed configuration below). "
      + "Also note that with the settings below you can define in which comment types the autocomplete is enabled.")]
    public bool EnableAutocomplete {
      get { return mEnableAutocomplete; }
      set {
        mEnableAutocomplete = value;
        ChangeReadOnlyAttributeFor(nameof(EnableParameterAutocompleteFor_param), !mEnableAutocomplete);
        ChangeReadOnlyAttributeFor(nameof(EnableParameterAutocompleteFor_tparam), !mEnableAutocomplete);
        ChangeReadOnlyAttributeFor(nameof(EnableParameterAutocompleteFor_p_a), !mEnableAutocomplete);
      }
    }

    private bool mEnableAutocomplete = true;
    
    [Category(FeaturesSubCategory)]
    [DisplayName("Enable quick info tooltips")]
    [Description("If enabled, hovering over a Doxygen command will display the help text of that command. "
      + "Note that with the settings below you can define in which comment types the quick info is enabled.")]
    public bool EnableQuickInfo { get; set; } = true;


    //----------------
    // Comment types

    // "\t" does not get printed, and is a hack to get the category to appear second in the property grid.
    public const string CommentTypesSubCategory = "\t\tTypes of comments with highlighting, autocomplete and quick infos";

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
    // Autocomplete detailed configuration

    // "\t" does not get printed, and is a hack to get the category to appear in the desired place in the property grid.
    public const string AutocompleteSubCategory = "\tIntelliSense detailed configuration";

    [Category(AutocompleteSubCategory)]
    [DisplayName("Autocomplete arguments for \"\\param\"")]
    [Description("If enabled, the extension lists the parameters of the next function or macro when typing after the Doxygen "
      + "command \"\\param\". Works only if \"Enable IntelliSense\" is enabled above.")]
    [ReadOnly(false)] // Required so that ChangeReadOnlyAttributeFor() works correctly.
    public bool EnableParameterAutocompleteFor_param { get; set; } = true;

    [Category(AutocompleteSubCategory)]
    [DisplayName("Autocomplete arguments for \"\\tparam\"")]
    [Description("If enabled, the extension lists the template parameters of the next class, struct, function or alias template (templated \"using\") "
      + "when typing after the Doxygen command \"\\tparam\". Works only if \"Enable IntelliSense\" is enabled above.")]
    [ReadOnly(false)] // Required so that ChangeReadOnlyAttributeFor() works correctly.
    public bool EnableParameterAutocompleteFor_tparam { get; set; } = true;

    [Category(AutocompleteSubCategory)]
    [DisplayName("Autocomplete arguments for \"\\p\" and \"\\a\"")]
    [Description("If enabled, the extension lists the parameters and template parameters of the function, macro, class, struct or alias template (templated \"using\") "
      + "when typing after the Doxygen commands \"\\p\" or \"\\a\". Works only if \"Enable IntelliSense\" is enabled above.")]
    [ReadOnly(false)] // Required so that ChangeReadOnlyAttributeFor() works correctly.
    public bool EnableParameterAutocompleteFor_p_a { get; set; } = true;


    //----------------
    // Commands configuration

    public const string CommandsConfigurationSubCategory = "Configuration of commands";

    [Category(CommandsConfigurationSubCategory)]
    [DisplayName("Individual Doxygen commands (use the \"...\" button!)")]
    [Description("Allows some configuration of individual Doxygen commands. Please do not edit the string in the grid directly. "
       + "Instead, use the \"...\" button on the right side of the row.")]
    // VS cannot serialize the list by itself, need to do it manually. Otherwise, the settings would not get saved.
    [TypeConverter(typeof(DoxygenCommandInConfigListSerialization))]
    [Editor(typeof(FixedElementsCollectionEditor), typeof(System.Drawing.Design.UITypeEditor))]
    public List<DoxygenCommandInConfig> DoxygenCommandsConfig { get; set; } = DoxygenCommands.DefaultCommandsInConfig;


    //----------------
    // Helpers

    /// <summary>
    /// Used to dynamically enable or disable some property in the options dialog depending on another property.
    /// </summary>
    private void ChangeReadOnlyAttributeFor(string property, bool isReadOnly)
    {
      // Based on https://stackoverflow.com/a/74010994/3740047
      PropertyDescriptor descriptor = TypeDescriptor.GetProperties(GetType())[property];
      Debug.Assert(descriptor != null);
      ReadOnlyAttribute attribute = descriptor?.Attributes[typeof(ReadOnlyAttribute)] as ReadOnlyAttribute;
      Debug.Assert(attribute != null);
      FieldInfo fieldToChange = attribute?.GetType().GetField(
        "isReadOnly",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Debug.Assert(fieldToChange != null);
      fieldToChange?.SetValue(attribute, isReadOnly);
    }

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
                      + $"JSON string that failed to get parsed:\n{valueAsString}",
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
