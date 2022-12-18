using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace VSDoxyHighlighter
{
  /// <summary>
  /// Represents the "General" options in the tools -> options menu.
  /// I.e. contains the settings of the extension that can be configured by the user, besides
  /// the classifier ones that Visual Studio automatically puts into the fonts & colors category.
  /// </summary>
  public class GeneralOptionsPage : DialogPage
  {
    // The string that appears in the tools -> options dialog when expanding the main node
    // for the VSDoxyHighlighter entry.
    public const string PageCategory = "General";

    //----------------
    // Comment types
    public const string CommentTypesSubCategory = "Types of comments with highlighting";

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"//\"")]
    [Description("Enables highlighting in comments starting with \"//\" (but for neither \"///\" nor \"//!\").")]
    public bool EnableInDoubleSlash { get; set; } = false;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"///\"")]
    [Description("Enables highlighting in comments starting with \"///\". Note that Visual Studio classifies these "
      + "as \"XML Doc comment\" and might use a different default text color or apply additional highlighting. See the "
      + "\"XML Doc comment\" entries in the \"Fonts and Colors\" settings.")]
    public bool EnableInTripleSlash { get; set; } = true;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"//!\"")]
    [Description("Enables highlighting in comments starting with \"//!\".")]
    public bool EnableInDoubleSlashExclamation { get; set; } = true;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"/*\"")]
    [Description("Enables highlighting in comments starting with \"/*\" (but for neither \"/**\" nor \"/*!\").")]
    public bool EnableInSlashStar { get; set; } = false;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"/**\"")]
    [Description("Enables highlighting in comments starting with \"/**\".")]
    public bool EnableInSlashStarStar { get; set; } = true;

    [Category(CommentTypesSubCategory)]
    [DisplayName("Enable in \"/*!\"")]
    [Description("Enables highlighting in comments starting with \"/*!\".")]
    public bool EnableInSlashStarExclamation { get; set; } = true;
  }
}
