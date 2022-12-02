using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace VSDoxyHighlighter
{
  /// <summary>
  /// Simple type to hold both an optional foreground and background color.
  /// </summary>
  public class TextColor 
  {
    public readonly Color? Foreground;
    public readonly Color? Background;

    public TextColor(Color? foreground, Color? background)
    {
      Foreground = foreground;
      Background = background;
    }
  }


  /// <summary>
  /// Manages the default colors of our classifications, suitable for the current Visual Studio's color theme.
  /// Thus, it provides access to the default colors for the current theme, and also updates them if the theme 
  /// of Visual Studio is changed by the user.
  /// 
  /// Note that the user settings are stored per theme in the registry.
  /// 
  /// An instance should be created via MEF.
  /// </summary>
  [Export]
  public class DefaultColors : IDisposable
  {
    // Returns the default colors for our extension's classifications, as suitable for the current color theme.
    public Dictionary<string, TextColor> GetDefaultColorsForCurrentTheme()
    {
      return GetColorsForTheme(mCurrentTheme);
    }


    DefaultColors() 
    {
      VSColorTheme.ThemeChanged += VSThemeChanged;
      mCurrentTheme = GetCurrentTheme();
    }

    void IDisposable.Dispose()
    {
      VSColorTheme.ThemeChanged -= VSThemeChanged;
    }


    private enum Theme
    {
      Light,
      Dark
    }

    static private Dictionary<string, TextColor> GetColorsForTheme(Theme theme)
    {
      switch (theme) {
        case Theme.Light:
          return cLightColors;
        case Theme.Dark:
          return cDarkColors;
        default:
          throw new System.Exception("Unknown Theme");
      }
    }

    // Event called by Visual Studio multiple times (!) when the user changes the color theme of Visual Studio.
    private void VSThemeChanged(ThemeChangedEventArgs e)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      var newTheme = GetCurrentTheme();
      if (newTheme != mCurrentTheme) {
        mCurrentTheme = newTheme;
        ThemeChangedImpl(newTheme);
      }
    }


    // Called when the Visual Studio theme changes. Responsible for switching out the default colors
    // of the classifications.
    //
    // Based on:
    // - https://stackoverflow.com/a/48993958/3740047
    // - https://github.com/dotnet/fsharp/blob/main/vsintegration/src/FSharp.Editor/Classification/ClassificationDefinitions.fs#L133
    // - https://github.com/fsprojects-archive/zzarchive-VisualFSharpPowerTools/blob/master/src/FSharpVSPowerTools/Commands/SymbolClassifiersProvider.cs
    private void ThemeChangedImpl(Theme newTheme)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      var fontAndColorStorage = ServiceProvider.GlobalProvider.GetService<SVsFontAndColorStorage, IVsFontAndColorStorage>();
      var fontAndColorCacheManager = ServiceProvider.GlobalProvider.GetService<SVsFontAndColorCacheManager, IVsFontAndColorCacheManager>();
      
      fontAndColorCacheManager.CheckCache(ref mFontAndColorCategoryGUID, out int _);

      if (fontAndColorStorage.OpenCategory(ref mFontAndColorCategoryGUID, (uint)__FCSTORAGEFLAGS.FCSF_READONLY) != VSConstants.S_OK) {
        throw new System.Exception("Failed to open font and color registry.");
      }

      IClassificationFormatMap formatMap = mClassificationFormatMapService.GetClassificationFormatMap(category: "text");

      try {
        formatMap.BeginBatchUpdate();

        ColorableItemInfo[] colorInfo = new ColorableItemInfo[1];
        foreach (var p in GetColorsForTheme(newTheme)) {
          string classificationTypeId = p.Key;
          TextColor newColor = p.Value;

          if (fontAndColorStorage.GetItem(classificationTypeId, colorInfo) != VSConstants.S_OK) { //comment from F# repo: "we don't touch the changes made by the user"
            IClassificationType classificationType = mClassificationTypeRegistryService.GetClassificationType(classificationTypeId);
            var oldProp = formatMap.GetTextProperties(classificationType);

            var foregroundBrush = newColor.Foreground == null ? null : new SolidColorBrush(newColor.Foreground.Value);
            var backgroundBrush = newColor.Background == null ? null : new SolidColorBrush(newColor.Background.Value);

            var newProp = TextFormattingRunProperties.CreateTextFormattingRunProperties(foregroundBrush, backgroundBrush, oldProp.Typeface, null, null,
              oldProp.TextDecorations, oldProp.TextEffects, oldProp.CultureInfo);

            formatMap.SetTextProperties(classificationType, newProp);
          }
        }
      }
      finally {
        formatMap.EndBatchUpdate();
        fontAndColorStorage.CloseCategory();

        if (fontAndColorCacheManager.ClearCache(ref mFontAndColorCategoryGUID) != VSConstants.S_OK) {
          throw new System.Exception("Failed to clear cache of FontAndColorCacheManager.");
        }
      }
    }


    private Theme GetCurrentTheme()
    {
      // We need to figure out if our extension should choose the default colors suitable for light or dark themes.
      // In principle we could explicitly retrieve the color theme currently active in Visual Studio. However, that
      // approach is fundamentally flawed: We could check if the theme is one of the default ones (dark, light, blue,
      // etc.), but Visual Studio supports installing additional themes. It is impossible to know all themes existing
      // out there. So, what we really want is to check if the dark or the light defaults are more suitable given the
      // text editor's background color.
      // However, the EnvironmentColors does not seem to contain an element for the text editor's background. So we
      // simply use the tool windows' background, as suggested also here: https://stackoverflow.com/a/48993958/3740047
      // The simplistic heuristic of just checking the blue color seems to work reasonably well. The magic threshold
      // was chosen to (hopefully) select the better value for the themes shown at https://devblogs.microsoft.com/visualstudio/custom-themes/
      var referenceColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
      return (referenceColor != null && referenceColor.B < 100) ? Theme.Dark : Theme.Light;
    }


    // Default colors for light color themes.
    static readonly Dictionary<string, TextColor> cLightColors = new Dictionary<string, TextColor> {
      { IDs.ID_command, new TextColor(Color.FromRgb(0, 75, 0), null) },
      { IDs.ID_parameter, new TextColor(Color.FromRgb(0, 80, 218), null) },
      { IDs.ID_title, new TextColor(Color.FromRgb(0, 0, 0), null) },
      { IDs.ID_warningKeyword, new TextColor(Color.FromRgb(200, 0, 0), null) },
      { IDs.ID_noteKeyword, new TextColor(Color.FromRgb(255, 155, 0), null) },
      { IDs.ID_emphasisMinor, new TextColor(Color.FromRgb(0, 75, 0), null) },
      { IDs.ID_emphasisMajor, new TextColor(Color.FromRgb(0, 75, 0), null) },
      { IDs.ID_inlineCode, new TextColor(Color.FromRgb(85, 85, 85), Color.FromRgb(235, 235, 235)) },
    };

    // Default colors for dark color themes.
    static readonly Dictionary<string, TextColor> cDarkColors = new Dictionary<string, TextColor> {
      { IDs.ID_command, new TextColor(Color.FromRgb(140, 203, 128), null) },
      { IDs.ID_parameter, new TextColor(Color.FromRgb(86, 156, 214), null) },
      { IDs.ID_title, new TextColor(Color.FromRgb(206, 206, 206), null) },
      { IDs.ID_warningKeyword, new TextColor(Color.FromRgb(255, 36, 23), null) },
      { IDs.ID_noteKeyword, new TextColor(Color.FromRgb(255, 155, 0), null) },
      { IDs.ID_emphasisMinor, new TextColor(Color.FromRgb(140, 203, 128), null) },
      { IDs.ID_emphasisMajor, new TextColor(Color.FromRgb(166, 215, 157), null) },
      { IDs.ID_inlineCode, new TextColor(Color.FromRgb(182, 182, 182), Color.FromRgb(51, 51, 51)) },
    };

    
    private Theme mCurrentTheme;

    // GUID of the category in which our classification items are placed (i.e. of the elements in the
    // fonts and colors settings of Visual Studio). Not just ours but all sorts of other items exist
    // in this category, too.
    // Can be found by installing our extension, modifying some of the colors of the classifications in
    // the Visual Studio's settings dialog, then exporting the settings and checking the resulting file.
    // The section about the modified entries contains the proper GUID.
    private const string cFontAndColorCategory = "75A05685-00A8-4DED-BAE5-E7A50BFA929A";
    Guid mFontAndColorCategoryGUID = new Guid(cFontAndColorCategory);

    [Import]
    private IClassificationFormatMapService mClassificationFormatMapService = null;

    [Import]
    private IClassificationTypeRegistryService mClassificationTypeRegistryService = null;
  }
}
