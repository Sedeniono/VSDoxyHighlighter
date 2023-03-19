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
    // In case a certain piece of text gets multiple classification format definitions, only one can
    // win. This is defined by the "Order" attribute. We use "after highest" to override the Viasfora
    // extension in comments.
    internal const string cFormatPriority = DefaultOrderings.Highest;

    protected FormatDefinitionBase(DefaultColors defaultColors, string ID, string displayName) 
    {
      if (defaultColors == null) {
        throw new VSDoxyHighlighterException("VSDoxyHighlighter: The 'DefaultColors' to a FormatDefinition is null");
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
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_command)]
  [Name(ClassificationIDs.ID_command)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)] 
  internal sealed class CommandFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public CommandFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_command, "VSDoxyHighlighter - Command")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_warningKeyword)]
  [Name(ClassificationIDs.ID_warningKeyword)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class WarningKeywordFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public WarningKeywordFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_warningKeyword, "VSDoxyHighlighter - Warning")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_noteKeyword)]
  [Name(ClassificationIDs.ID_noteKeyword)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class NoteKeywordFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public NoteKeywordFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_noteKeyword, "VSDoxyHighlighter - Note")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_exceptions)]
  [Name(ClassificationIDs.ID_exceptions)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class ExceptionFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public ExceptionFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_exceptions, "VSDoxyHighlighter - Exceptions")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_parameter1)]
  [Name(ClassificationIDs.ID_parameter1)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class ParameterFormat1 : FormatDefinitionBase
  {
    [ImportingConstructor]
    public ParameterFormat1(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_parameter1, "VSDoxyHighlighter - Parameter 1")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_parameter2)]
  [Name(ClassificationIDs.ID_parameter2)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class ParameterFormat2 : FormatDefinitionBase
  {
    [ImportingConstructor]
    public ParameterFormat2(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_parameter2, "VSDoxyHighlighter - Parameter 2")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_emphasisMinor)]
  [Name(ClassificationIDs.ID_emphasisMinor)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class EmphasisMinorFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public EmphasisMinorFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_emphasisMinor, "VSDoxyHighlighter - Emphasis (minor)")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_emphasisMajor)]
  [Name(ClassificationIDs.ID_emphasisMajor)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class EmphasisMajorFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public EmphasisMajorFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_emphasisMajor, "VSDoxyHighlighter - Emphasis (major)")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_inlineCode)]
  [Name(ClassificationIDs.ID_inlineCode)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class InlineCodeFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public InlineCodeFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_inlineCode, "VSDoxyHighlighter - Inline code")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_title)]
  [Name(ClassificationIDs.ID_title)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class TitleFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public TitleFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_title, "VSDoxyHighlighter - Title")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_strikethrough)]
  [Name(ClassificationIDs.ID_strikethrough)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class StrikethroughFormat : FormatDefinitionBase
  {
    [ImportingConstructor]
    public StrikethroughFormat(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_strikethrough, "VSDoxyHighlighter - Strikethrough")
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


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_generic1)]
  [Name(ClassificationIDs.ID_generic1)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class Generic1Format : FormatDefinitionBase
  {
    [ImportingConstructor]
    public Generic1Format(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_generic1, "VSDoxyHighlighter - Generic 1")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_generic2)]
  [Name(ClassificationIDs.ID_generic2)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class Generic2Format : FormatDefinitionBase
  {
    [ImportingConstructor]
    public Generic2Format(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_generic2, "VSDoxyHighlighter - Generic 2")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_generic3)]
  [Name(ClassificationIDs.ID_generic3)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class Generic3Format : FormatDefinitionBase
  {
    [ImportingConstructor]
    public Generic3Format(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_generic3, "VSDoxyHighlighter - Generic 3")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_generic4)]
  [Name(ClassificationIDs.ID_generic4)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class Generic4Format : FormatDefinitionBase
  {
    [ImportingConstructor]
    public Generic4Format(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_generic4, "VSDoxyHighlighter - Generic 4")
    {
    }
  }


  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_generic5)]
  [Name(ClassificationIDs.ID_generic5)]
  [UserVisible(true)]
  [Order(After = FormatDefinitionBase.cFormatPriority)]
  internal sealed class Generic5Format : FormatDefinitionBase
  {
    [ImportingConstructor]
    public Generic5Format(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_generic5, "VSDoxyHighlighter - Generic 5")
    {
    }
  }

}
