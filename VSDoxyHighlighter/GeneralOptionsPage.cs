﻿using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

namespace VSDoxyHighlighter
{
  /// <summary>
  /// Represents a single Doxygen command that can be configured by the user.
  /// Note that its members are serialized to and from a string!
  /// </summary>
  [DataContract]
  public class DoxygenCommandInConfig
  {
    [Category("Command")]
    [DisplayName("Command")]
    [Description("The Doxygen command that gets configured.")]
    [ReadOnly(true)]
    [DataMember(Name = "Cmd", Order = 0)]
    public string Command { get; set; } = "NEW_COMMAND";

    private const string PropertiesCategory = "Properties";

    [Category(PropertiesCategory)]
    [DisplayName("Classification")]
    [Description("Specifies which classification from the fonts & colors dialog is used for this command.")]
    [DataMember(Name = "Clsif", Order = 1)]
    public DoxygenCommandType Classification { get; set; } = DoxygenCommandType.Command1;

    //[Category(PropertiesCategory)]
    //[DisplayName("Arguments")]
    //[Description("The arguments that the Doxygen command expects. This is currently readonly and just displayed for information purposes.")]
    //[ReadOnly(true)]
    //[TypeConverter(typeof(StringConverter))]
    //public ITuple FragmentTypes { get; set; }

//#error Allow config of fragment types? I think that should be trivial...
//#error Also check all the TODO comments

    // Get a sensible display in the CollectionEditor.
    public override string ToString()
    {
      return Command;
    }
  }


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


  /// <summary>
  /// Represents the "General" options in the tools -> options menu.
  /// I.e. contains the settings of the extension that can be configured by the user, besides
  /// the classifier ones that Visual Studio automatically puts into the fonts & colors category.
  /// </summary>
  // The GUID appears as "Category" in the vssettings file.
  [Guid("c3a8c4bb-8e5a-49a9-b3c3-343ed507f0f9")]
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
    public const string FeaturesSubCategory = "Features";

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
    public const string CommentTypesSubCategory = "Types of comments with highlighting and autocomplete";

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"//\"")]
    [Description("Enables the extension in comments starting with \"//\" (but for neither \"///\" nor \"//!\").")]
    public bool EnableInDoubleSlash { get; set; } = false;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"///\"")]
    [Description("Enables the extension in comments starting with \"///\". Note that Visual Studio classifies these "
      + "as \"XML Doc comment\" and might use a different default text color or apply additional highlighting. See the "
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
    [TypeConverter(typeof(SerializationViaStringConverter))]
    [Editor(typeof(DoxygenCommandCollectionEditor), typeof(System.Drawing.Design.UITypeEditor))]
    public List<DoxygenCommandInConfig> DoxygenCommandsConfig { get; set; } = DoxygenCommands.DefaultDoxygenCommandsInConfig;


    //----------------
    // Helpers

    /// <summary>
    ///  Serializes some type T to and from a string, such that Visual Studio's saving facilities can save and load the data.
    /// </summary>
    //public class SerializationViaStringConverter<T> : TypeConverter where T : class
    //{
    //  public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    //  {
    //    return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    //  }

    //  public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
    //  {
    //    return destinationType == typeof(T) || base.CanConvertTo(context, destinationType);
    //  }

    //  public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    //  {
    //    if (value is string valueAsString) {
    //      using (var stringReader = new System.IO.StringReader(valueAsString)) {
    //        var serializer = new XmlSerializer(typeof(T));
    //        return serializer.Deserialize(stringReader) as T;
    //      }
    //    }
    //    else {
    //      return base.ConvertFrom(context, culture, value);
    //    }
    //  }

    //  public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
    //  {
    //    if (destinationType == typeof(string) && value is T valueAsT) {
    //      using (var stringWriter = new System.IO.StringWriter()) {
    //        var serializer = new XmlSerializer(valueAsT.GetType());
    //        serializer.Serialize(stringWriter, valueAsT);
    //        return stringWriter.ToString();
    //      }
    //    }
    //    else {
    //      return base.ConvertTo(context, culture, value, destinationType);
    //    }
    //  }
    //}

    public class SerializationViaStringConverter : TypeConverter
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
        if (value is string valueAsString) {
          using (var memStream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(valueAsString))) {
            try {
              var serializer = new DataContractJsonSerializer(typeof(List<DoxygenCommandInConfig>));
              return (List<DoxygenCommandInConfig>)serializer.ReadObject(memStream);
            }
            catch (Exception ex) {
              ActivityLog.LogWarning("VSDoxyHighlighter", $"Conversion from a string failed. Exception message: {ex.ToString()}");
              // TODO: Is restoring the defaults really a good idea? Can we maybe display a message box and ask the user?
              return DoxygenCommands.DefaultDoxygenCommandsInConfig;
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
    }

    /// <summary>
    /// CollectionEditor that is used for the configuration of individual Doxygen commands.
    /// </summary>
    private class DoxygenCommandCollectionEditor : CollectionEditor
    {
      public DoxygenCommandCollectionEditor(Type type)
         : base(type)
      {
      }

      protected override CollectionForm CreateCollectionForm()
      {
        CollectionForm form = base.CreateCollectionForm();
        
        // For now, we do not support adding or removing commands. So hide the corresponding buttons.
        ((ButtonBase)form.Controls.Find("addButton", true).First()).Hide();
        ((ButtonBase)form.Controls.Find("removeButton", true).First()).Hide();

        // The order of the commands does not matter. So hide the corresponding buttons.
        ((ButtonBase)form.Controls.Find("upButton", true).First()).Hide();
        ((ButtonBase)form.Controls.Find("downButton", true).First()).Hide();

        return form;
      }
    }

  }
}
