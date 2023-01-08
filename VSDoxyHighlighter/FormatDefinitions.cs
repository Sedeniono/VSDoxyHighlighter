using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;

namespace VSDoxyHighlighter
{
  //===========================================================
  // IFormatDefinition
  //===========================================================

  public interface IFormatDefinition 
  {
    void Reinitialize();
  }


  //===========================================================
  // FormatDefinitionBase
  //===========================================================

  internal abstract class FormatDefinitionBase : ClassificationFormatDefinition, IFormatDefinition
  {
    public abstract void Reinitialize();

    protected FormatDefinitionBase(DefaultColors defaultColors, string displayName) 
    {
      if (defaultColors == null) {
        throw new System.ArgumentNullException("VSDoxyHighlighter: The 'DefaultColors' to a FormatDefinition is null");
      }
      mDefaultColors = defaultColors;
      mDefaultColors.RegisterFormatDefinition(this);

      DisplayName = displayName;
    }

    protected readonly DefaultColors mDefaultColors;
  }


  //===========================================================
  // Format definitions
  //===========================================================

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_command)]
  [Name(IDs.ID_command)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class CommandFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public CommandFormat(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Command")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_command];
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
  internal sealed class WarningKeywordFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public WarningKeywordFormat(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Warning")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_warningKeyword];
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
  internal sealed class NoteKeywordFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public NoteKeywordFormat(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Note")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_noteKeyword];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = true;
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_parameter1)]
  [Name(IDs.ID_parameter1)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class ParameterFormat1 : FormatDefinitionBase
  {
    [ImportingConstructor]
    public ParameterFormat1(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Parameter 1")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_parameter1];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = true;
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_parameter2)]
  [Name(IDs.ID_parameter2)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class ParameterFormat2 : FormatDefinitionBase
  {
    [ImportingConstructor]
    public ParameterFormat2(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Parameter 2")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_parameter2];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = false;
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_emphasisMinor)]
  [Name(IDs.ID_emphasisMinor)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class EmphasisMinorFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public EmphasisMinorFormat(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Emphasis (minor)")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_emphasisMinor];
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
  internal sealed class EmphasisMajorFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public EmphasisMajorFormat(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Emphasis (major)")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_emphasisMajor];
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
  internal sealed class InlineCodeFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public InlineCodeFormat(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Inline code")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_inlineCode];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_title)]
  [Name(IDs.ID_title)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class TitleFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public TitleFormat(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Title")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextColor color = mDefaultColors.GetDefaultColorsForCurrentTheme()[IDs.ID_title];
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
  internal sealed class StrikethroughFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public StrikethroughFormat(DefaultColors defaultColors)
      : base(defaultColors, "VSDoxyHighlighter - Strikethrough")
    {
      Reinitialize();
    }

    public override void Reinitialize()
    {
      TextDecorations = new TextDecorationCollection {
        new TextDecoration(
          TextDecorationLocation.Strikethrough, null, 0.0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended)
      };
    }
  }
}
