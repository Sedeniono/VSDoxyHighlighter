using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;

namespace VSDoxyHighlighter
{
  //===========================================================
  // IClassificationDefinition
  //===========================================================

  public interface IClassificationDefinition 
  {
    void Reinitialize();
  }


  //===========================================================
  // ClassificationDefinitionBase
  //===========================================================

  internal abstract class ClassificationDefinitionBase : ClassificationFormatDefinition, IClassificationDefinition
  {
    // In case a certain piece of text gets multiple classification format definitions, only one can
    // win. This is defined by the "Order" attribute. We use "after highest" to override the Viasfora
    // extension in comments.
    internal const string cPriority = DefaultOrderings.Highest;

    protected ClassificationDefinitionBase(DefaultColors defaultColors, string ID, string displayName) 
    {
      if (defaultColors == null) {
        throw new VSDoxyHighlighterException("VSDoxyHighlighter: The 'DefaultColors' to a FormatDefinition is null");
      }

      mID = ID;

      mDefaultColors = defaultColors;
      mDefaultColors.RegisterClassificationDefinition(this);

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
  // Actual classification format definitions
  //===========================================================

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = ClassificationIDs.ID_command)]
  [Name(ClassificationIDs.ID_command)]
  [UserVisible(true)]
  [Order(After = ClassificationDefinitionBase.cPriority)] 
  internal sealed class CommandFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class WarningKeywordFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class NoteKeywordFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class ExceptionFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class ParameterFormat1 : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class ParameterFormat2 : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class EmphasisMinorFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class EmphasisMajorFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class InlineCodeFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class TitleFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class StrikethroughFormat : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class Generic1Format : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class Generic2Format : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class Generic3Format : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class Generic4Format : ClassificationDefinitionBase
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
  [Order(After = ClassificationDefinitionBase.cPriority)]
  internal sealed class Generic5Format : ClassificationDefinitionBase
  {
    [ImportingConstructor]
    public Generic5Format(DefaultColors defaultColors)
      : base(defaultColors, ClassificationIDs.ID_generic5, "VSDoxyHighlighter - Generic 5")
    {
    }
  }


  //===========================================================
  // CommentClassificationDefinitions
  //===========================================================

  /// <summary>
  /// Tells Visual Studio via MEF about the classifications provided by the extension.
  /// </summary>
  internal static class CommentClassificationDefinitions
  {
#pragma warning disable 169
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_command)]
    private static ClassificationTypeDefinition typeDefinitionForCommand;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_warningKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForWarningKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_noteKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForNoteKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_exceptions)]
    private static ClassificationTypeDefinition typeDefinitionForExceptions;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_parameter1)]
    private static ClassificationTypeDefinition typeDefinitionForParameter1;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_parameter2)]
    private static ClassificationTypeDefinition typeDefinitionForParameter2;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_emphasisMinor)]
    private static ClassificationTypeDefinition typeDefinitionForEmphasisMinor;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_emphasisMajor)]
    private static ClassificationTypeDefinition typeDefinitionForEmphasisMajor;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_strikethrough)]
    private static ClassificationTypeDefinition typeDefinitionForStrikethrough;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_inlineCode)]
    private static ClassificationTypeDefinition typeDefinitionForInlineCode;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_title)]
    private static ClassificationTypeDefinition typeDefinitionForTitle;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic1)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric1;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic2)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric2;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic3)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric3;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic4)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric4;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationIDs.ID_generic5)]
    private static ClassificationTypeDefinition typeDefinitionForGeneric5;
#pragma warning restore 169
  }

}
