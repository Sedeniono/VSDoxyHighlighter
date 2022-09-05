﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace VSDoxyHighlighter
{
  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_normalKeyword)]
  [Name(IDs.ID_normalKeyword)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class NormalKeywordFormat : ClassificationFormatDefinition
  {
    public NormalKeywordFormat()
    {
      DisplayName = "DoxyHighlighter - Normal keyword";
      ForegroundColor = Color.FromRgb(0, 75, 0);
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
    public WarningKeywordFormat()
    {
      DisplayName = "DoxyHighlighter - Warnings";
      ForegroundColor = Color.FromRgb(200, 0, 0);
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
    public NoteKeywordFormat()
    {
      DisplayName = "DoxyHighlighter - Notes";
      ForegroundColor = Color.FromRgb(255, 155, 0);
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
    public ParameterFormat()
    {
      DisplayName = "DoxyHighlighter - Parameter";
      ForegroundColor = Color.FromRgb(0, 80, 218);
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
    public EmphasisMinorFormat()
    {
      DisplayName = "DoxyHighlighter - Emphasis (minor)";
      ForegroundColor = Color.FromRgb(87, 166, 74);
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
    public EmphasisMajorFormat()
    {
      DisplayName = "DoxyHighlighter - Emphasis (major)";
      ForegroundColor = Color.FromRgb(0, 75, 0);
      IsBold  = true;
    }
  }

  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = IDs.ID_inlineCode)]
  [Name(IDs.ID_inlineCode)]
  [UserVisible(true)]
  [Order(After = DefaultOrderings.Highest)] // After highest required to override Viasfora in comments
  internal sealed class InlineCodeFormat : ClassificationFormatDefinition
  {
    public InlineCodeFormat()
    {
      DisplayName = "DoxyHighlighter - Inline code";
      ForegroundColor = Color.FromRgb(104, 104, 104);
      BackgroundColor = Color.FromRgb(235, 235, 235);
    }
  }
}