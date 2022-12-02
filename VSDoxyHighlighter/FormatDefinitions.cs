using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;

namespace VSDoxyHighlighter
{
  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_command)]
  [Name(IDs.ID_command)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class CommandFormat : ClassificationFormatDefinition
  {
    [ImportingConstructor]
    public CommandFormat(DefaultColors defaultColors)
    {
      DisplayName = "DoxyHighlighter - Command";
      TextColor color = defaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_command];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = true;
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_warningKeyword)]
  [Name(IDs.ID_warningKeyword)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class WarningKeywordFormat : ClassificationFormatDefinition
  {
    [ImportingConstructor]
    public WarningKeywordFormat(DefaultColors defaultColors)
    {
      DisplayName = "DoxyHighlighter - Warnings";
      TextColor color = defaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_warningKeyword];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = true;
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_noteKeyword)]
  [Name(IDs.ID_noteKeyword)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class NoteKeywordFormat : ClassificationFormatDefinition
  {
    [ImportingConstructor]
    public NoteKeywordFormat(DefaultColors defaultColors)
    {
      DisplayName = "DoxyHighlighter - Notes";
      TextColor color = defaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_noteKeyword];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = true;
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_parameter)]
  [Name(IDs.ID_parameter)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class ParameterFormat : ClassificationFormatDefinition
  {
    [ImportingConstructor]
    public ParameterFormat(DefaultColors defaultColors)
    {
      DisplayName = "DoxyHighlighter - Parameter";
      TextColor color = defaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_parameter];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = true;
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_emphasisMinor)]
  [Name(IDs.ID_emphasisMinor)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class EmphasisMinorFormat : ClassificationFormatDefinition
  {
    [ImportingConstructor]
    public EmphasisMinorFormat(DefaultColors defaultColors)
    {
      DisplayName = "DoxyHighlighter - Emphasis (minor)";
      TextColor color = defaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_emphasisMinor];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsItalic = true;
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_emphasisMajor)]
  [Name(IDs.ID_emphasisMajor)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class EmphasisMajorFormat : ClassificationFormatDefinition
  {
    [ImportingConstructor]
    public EmphasisMajorFormat(DefaultColors defaultColors)
    {
      DisplayName = "DoxyHighlighter - Emphasis (major)";
      TextColor color = defaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_emphasisMajor];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = true;
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_inlineCode)]
  [Name(IDs.ID_inlineCode)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class InlineCodeFormat : ClassificationFormatDefinition
  {
    [ImportingConstructor]
    public InlineCodeFormat(DefaultColors defaultColors)
    {
      DisplayName = "DoxyHighlighter - Inline code";
      TextColor color = defaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_inlineCode];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_title)]
  [Name(IDs.ID_title)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class TitleFormat : ClassificationFormatDefinition
  {
    [ImportingConstructor]
    public TitleFormat(DefaultColors defaultColors)
    {
      DisplayName = "DoxyHighlighter - Title";
      TextColor color = defaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_title];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = true;
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_strikethrough)]
  [Name(IDs.ID_strikethrough)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class StrikethroughFormat : ClassificationFormatDefinition
  {
    public StrikethroughFormat()
    {
      DisplayName = "DoxyHighlighter - Strikethrough";

      // Note: No need for DefaultColors. We just add the strikethrough, but leave the text color unchanged.

      TextDecorations = new TextDecorationCollection();
      TextDecorations.Add(new TextDecoration(
          TextDecorationLocation.Strikethrough, null, 0.0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended));
    }
  }
}
