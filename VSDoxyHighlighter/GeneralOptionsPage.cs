using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace VSDoxyHighlighter
{
  /// <summary>
  /// Represents the "General" options in the tools -> options menu.
  /// I.e. contains the settings of the extension that can be configured by the user, besides
  /// the classifier ones that Visual Studio automatically puts into the fonts & colors category.
  /// </summary>
  // The GUID appears as "Category" in the vssettings file.
  [Guid("c3a8c4bb-8e5a-49a9-b3c3-343ed507f0f9")]
  public class GeneralOptionsPage : DialogPage
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
    [DisplayName("Enable autocomplete")]
    [Description("Enables the autocomplete of commands while typing in comments (\"IntelliSense\"). "
      + "Note that with the other settings you can define in which comment types the highlighting is enabled.")]
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
  }
}
