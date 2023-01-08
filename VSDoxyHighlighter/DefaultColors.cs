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
using System.Windows;
using System.Windows.Media;

namespace VSDoxyHighlighter
{
  /// <summary>
  /// Simple type to formatting information about some text.
  /// </summary>
  public class TextProperties 
  {
    public readonly Color? Foreground;
    public readonly Color? Background;
    public readonly bool IsBold;
    public readonly bool IsItalic;

    public TextProperties(Color? foreground, Color? background, bool isBold, bool isItalic)
    {
      Foreground = foreground;
      Background = background;
      IsBold = isBold;
      IsItalic = isItalic;
    }
  }


  /// <summary>
  /// Manages the default colors and formatting of our classifications, suitable for the current Visual Studio's color theme.
  /// Thus, it provides access to the default formatting for the current theme, and also updates them if the theme 
  /// of Visual Studio is changed by the user.
  /// 
  /// Note that the user settings are stored per theme in the registry.
  /// 
  /// An instance should be created via MEF.
  /// </summary>
  [Export]
  public class DefaultColors : IDisposable
  {
    DefaultColors() 
    {
      VSColorTheme.ThemeChanged += VSThemeChanged;
      mCurrentTheme = GetCurrentTheme();
    }


    public void Dispose()
    {
      if (mDisposed) {
        return;
      }
      mDisposed = true;
      VSColorTheme.ThemeChanged -= VSThemeChanged;
    }


    /// <summary>
    /// Returns the default colors for our extension's classifications, as suitable for the current color theme. 
    /// </summary>
    public Dictionary<string, TextProperties> GetDefaultFormattingForCurrentTheme()
    {
      return GetDefaultFormattingForTheme(mCurrentTheme);
    }


    public void RegisterFormatDefinition(IFormatDefinition f) 
    {
      mFormatDefinitions.Add(f);
    }


    private enum Theme
    {
      Light,
      Dark
    }

    static private Dictionary<string, TextProperties> GetDefaultFormattingForTheme(Theme theme)
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
        mCurrentTheme = newTheme; // Important: We indirectly access mCurrentTheme during the update, so set it before.
        ThemeChangedImpl();
      }
    }


    // Called when the Visual Studio theme changes. Responsible for switching out the default colors
    // of the classifications.
    //
    // Based on:
    // - https://stackoverflow.com/a/48993958/3740047
    // - https://github.com/dotnet/fsharp/blob/main/vsintegration/src/FSharp.Editor/Classification/ClassificationDefinitions.fs#L133
    // - https://github.com/fsprojects-archive/zzarchive-VisualFSharpPowerTools/blob/master/src/FSharpVSPowerTools/Commands/SymbolClassifiersProvider.cs
    private void ThemeChangedImpl()
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
        foreach (var p in GetDefaultFormattingForTheme(mCurrentTheme)) {
          string classificationTypeId = p.Key;
          TextProperties newColor = p.Value;

          if (fontAndColorStorage.GetItem(classificationTypeId, colorInfo) != VSConstants.S_OK) { //comment from F# repo: "we don't touch the changes made by the user"
            IClassificationType classificationType = mClassificationTypeRegistryService.GetClassificationType(classificationTypeId);
            var oldProp = formatMap.GetTextProperties(classificationType);
            var oldTypeface = oldProp.Typeface;

            var foregroundBrush = newColor.Foreground == null ? null : new SolidColorBrush(newColor.Foreground.Value);
            var backgroundBrush = newColor.Background == null ? null : new SolidColorBrush(newColor.Background.Value);

            var newFontStyle = newColor.IsItalic ? FontStyles.Italic : FontStyles.Normal;
            var newWeight = newColor.IsBold ? FontWeights.Bold : FontWeights.Normal;
            var newTypeface = new Typeface(oldTypeface.FontFamily, newFontStyle, newWeight, oldTypeface.Stretch);

            var newProp = TextFormattingRunProperties.CreateTextFormattingRunProperties(
              foregroundBrush, backgroundBrush, newTypeface, null, null,
              oldProp.TextDecorations, oldProp.TextEffects, oldProp.CultureInfo);

            formatMap.SetTextProperties(classificationType, newProp);
          }
        }

        // Also update all of our ClassificationFormatDefinition values with the new values.
        // Without this, changing the theme does not reliably update the colors: Sometimes after restarting VS, we get
        // the wrong colors. For example, when switching from the dark to the light theme, we often end up with the colors
        // of the dark theme after a VS restart.
        // From what I could understand: The call fontAndColorCacheManager.ClearCache() below deletes the registry key
        //     "Software\Microsoft\VisualStudio\17.0_4d51a943Exp\FontAndColors\Cache\{75A05685-00A8-4DED-BAE5-E7A50BFA929A}\ItemAndFontInfo"
        // which is the cache of the font and colors. After our function here finishes, some Visual Studio component
        // sometimes (but not always) immediately updates the font and color cache. I.e. it calls something like
        //     fontAndColorStorage.OpenCategory(ref mFontAndColorCategoryGUID, (uint)__FCSTORAGEFLAGS.FCSF_READONLY | (uint)__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS).
        // Note the "FCSF_LOADDEFAULTS". This causes Visual Studio to re-create the registry key. However, apparently
        // it does not use the colors from the updated formatMap, but instead the colors set on the ClassificationFormatDefinition,
        // which were not yet updated so far. Thus, changing the theme, changes the displayed colors immediately (because we update
        // the formatMap), but the registry cache ends up with the wrong colors. After a restart of VS, it uses the cached values
        // and therefore we get the wrong colors.
        // By changing the colors also on the ClassificationFormatDefinition, the issue appears to be fixed.
        foreach (IFormatDefinition f in mFormatDefinitions) {
          f.Reinitialize();
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
    static readonly Dictionary<string, TextProperties> cLightColors = new Dictionary<string, TextProperties> {
      { IDs.ID_command,        new TextProperties(foreground: Color.FromRgb(0, 75, 0),    background: null, isBold: true,  isItalic: false) },
      { IDs.ID_parameter1,     new TextProperties(foreground: Color.FromRgb(0, 80, 218),  background: null, isBold: true,  isItalic: false) },
      { IDs.ID_parameter2,     new TextProperties(foreground: Color.FromRgb(0, 80, 218),  background: null, isBold: false, isItalic: false) },
      { IDs.ID_title,          new TextProperties(foreground: Color.FromRgb(0, 0, 0),     background: null, isBold: true,  isItalic: false) },
      { IDs.ID_warningKeyword, new TextProperties(foreground: Color.FromRgb(200, 0, 0),   background: null, isBold: true,  isItalic: false) },
      { IDs.ID_noteKeyword,    new TextProperties(foreground: Color.FromRgb(255, 155, 0), background: null, isBold: true,  isItalic: false) },
      { IDs.ID_emphasisMinor,  new TextProperties(foreground: Color.FromRgb(0, 75, 0),    background: null, isBold: false, isItalic: true) },
      { IDs.ID_emphasisMajor,  new TextProperties(foreground: Color.FromRgb(0, 75, 0),    background: null, isBold: true,  isItalic: false) },
      { IDs.ID_strikethrough,  new TextProperties(foreground: null,                       background: null, isBold: false, isItalic: false) },
      { IDs.ID_inlineCode,     new TextProperties(foreground: Color.FromRgb(85, 85, 85),  background: Color.FromRgb(235, 235, 235), isBold: false, isItalic: false) },
    };

    // Default colors for dark color themes.
    static readonly Dictionary<string, TextProperties> cDarkColors = new Dictionary<string, TextProperties> {
      { IDs.ID_command,        new TextProperties(foreground: Color.FromRgb(140, 203, 128), background: null, isBold: true,  isItalic: false) },
      { IDs.ID_parameter1,     new TextProperties(foreground: Color.FromRgb(86, 156, 214),  background: null, isBold: true,  isItalic: false) },
      { IDs.ID_parameter2,     new TextProperties(foreground: Color.FromRgb(86, 156, 214),  background: null, isBold: false, isItalic: false) },
      { IDs.ID_title,          new TextProperties(foreground: Color.FromRgb(206, 206, 206), background: null, isBold: true,  isItalic: false) },
      { IDs.ID_warningKeyword, new TextProperties(foreground: Color.FromRgb(255, 36, 23),   background: null, isBold: true,  isItalic: false) },
      { IDs.ID_noteKeyword,    new TextProperties(foreground: Color.FromRgb(255, 155, 0),   background: null, isBold: true,  isItalic: false) },
      { IDs.ID_emphasisMinor,  new TextProperties(foreground: Color.FromRgb(140, 203, 128), background: null, isBold: false, isItalic: true) },
      { IDs.ID_emphasisMajor,  new TextProperties(foreground: Color.FromRgb(166, 215, 157), background: null, isBold: true,  isItalic: false) },
      { IDs.ID_strikethrough,  new TextProperties(foreground: null,                         background: null, isBold: false, isItalic: false) },
      { IDs.ID_inlineCode,     new TextProperties(foreground: Color.FromRgb(200, 200, 200), background: Color.FromRgb(51, 51, 51), isBold: false, isItalic: false) },
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

    private List<IFormatDefinition> mFormatDefinitions = new List<IFormatDefinition>();

    private bool mDisposed = false;
  }
}
