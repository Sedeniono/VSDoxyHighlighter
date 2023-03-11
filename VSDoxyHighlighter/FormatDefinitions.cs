﻿using Microsoft.VisualStudio.Text;
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
    // In case a certain piece of text gets multiple classification format definitions, only one can
    // win. This is defined by the "Order" attribute. We use "after highest" to override the Viasfora
    // extension in comments.
    internal const string cFormatPriority = DefaultOrderings.Highest;

    protected FormatDefinitionBase(DefaultColors defaultColors, string ID, string displayName) 
    {
      if (defaultColors == null) {
        throw new System.ArgumentNullException("VSDoxyHighlighter: The 'DefaultColors' to a FormatDefinition is null");
      }

      mID = ID;

      mDefaultColors = defaultColors;
      mDefaultColors.RegisterFormatDefinition(this);

      DisplayName = displayName;

      Reinitialize();
    }

    public virtual void Reinitialize()
    {
      TextProperties color = mDefaultColors.GetDefaultFormattingForCurrentTheme()[mID];
      ForegroundColor = color.Foreground;
      BackgroundColor = color.Background;
      IsBold = color.IsBold;
      IsItalic = color.IsItalic;
    }

    protected readonly DefaultColors mDefaultColors;
    protected readonly string mID;
  }


  //===========================================================
  // Format definitions
  //===========================================================

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_command1)]
  [Name(IDs.ID_command1)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)] 
  internal sealed class Command1Format : FormatDefinitionBase
  {
    [ImportingConstructor]
    public Command1Format(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_command1, "VSDoxyHighlighter - Command 1")
    {
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_command2)]
  [Name(IDs.ID_command2)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class Command2Format : FormatDefinitionBase
  {
    [ImportingConstructor]
    public Command2Format(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_command2, "VSDoxyHighlighter - Command 2")
    {
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_command3)]
  [Name(IDs.ID_command3)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class Command3Format : FormatDefinitionBase
  {
    [ImportingConstructor]
    public Command3Format(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_command3, "VSDoxyHighlighter - Command 3")
    {
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_warningKeyword)]
  [Name(IDs.ID_warningKeyword)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class WarningKeywordFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public WarningKeywordFormat(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_warningKeyword, "VSDoxyHighlighter - Warning")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_noteKeyword)]
  [Name(IDs.ID_noteKeyword)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class NoteKeywordFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public NoteKeywordFormat(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_noteKeyword, "VSDoxyHighlighter - Note")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_exceptions)]
  [Name(IDs.ID_exceptions)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class ExceptionFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public ExceptionFormat(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_exceptions, "VSDoxyHighlighter - Exceptions")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_parameter1)]
  [Name(IDs.ID_parameter1)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class ParameterFormat1 : FormatDefinitionBase
  {
    [ImportingConstructor]
    public ParameterFormat1(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_parameter1, "VSDoxyHighlighter - Parameter 1")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_parameter2)]
  [Name(IDs.ID_parameter2)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class ParameterFormat2 : FormatDefinitionBase
  {
    [ImportingConstructor]
    public ParameterFormat2(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_parameter2, "VSDoxyHighlighter - Parameter 2")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_emphasisMinor)]
  [Name(IDs.ID_emphasisMinor)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class EmphasisMinorFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public EmphasisMinorFormat(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_emphasisMinor, "VSDoxyHighlighter - Emphasis (minor)")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_emphasisMajor)]
  [Name(IDs.ID_emphasisMajor)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class EmphasisMajorFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public EmphasisMajorFormat(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_emphasisMajor, "VSDoxyHighlighter - Emphasis (major)")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_inlineCode)]
  [Name(IDs.ID_inlineCode)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class InlineCodeFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public InlineCodeFormat(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_inlineCode, "VSDoxyHighlighter - Inline code")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_title)]
  [Name(IDs.ID_title)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class TitleFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public TitleFormat(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_title, "VSDoxyHighlighter - Title")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_strikethrough)]
  [Name(IDs.ID_strikethrough)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class StrikethroughFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public StrikethroughFormat(DefaultColors defaultColors)
      : base(defaultColors, IDs.ID_strikethrough, "VSDoxyHighlighter - Strikethrough")
    {
    }

    public override void Reinitialize()
    {
      base.Reinitialize();

      TextDecorations = new TextDecorationCollection {
        new TextDecoration(
          TextDecorationLocation.Strikethrough, null, 0.0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended)
      };
    }
  }
}
