using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;


namespace VSDoxyHighlighter
{
  public static class IDs
  {
    public const string ID_normalKeyword = "Doxy/Normal";
    public const string ID_warningKeyword = "Doxy/Warning";
    public const string ID_noteKeyword = "Doxy/Note";
    public const string ID_parameter = "Doxy/Parameter";
    public const string ID_emphasisMinor = "Doxy/EmphasisMinor";
    public const string ID_emphasisMajor = "Doxy/EmphasisMajor";
    public const string ID_strikethrough = "Doxy/Strikethrough";
    public const string ID_inlineCode = "Doxy/InlineCode";
    public const string ID_title = "Doxy/Title";
  }




  internal static class TestClassifierClassificationDefinition
  {
#pragma warning disable 169
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_normalKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForNormalKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_warningKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForWarningKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_noteKeyword)]
    private static ClassificationTypeDefinition typeDefinitionForNoteKeyword;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_parameter)]
    private static ClassificationTypeDefinition typeDefinitionForParameter;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_emphasisMinor)]
    private static ClassificationTypeDefinition typeDefinitionForEmphasisMinor;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_emphasisMajor)]
    private static ClassificationTypeDefinition typeDefinitionForEmphasisMajor;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_strikethrough)]
    private static ClassificationTypeDefinition typeDefinitionForStrikethrough;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_inlineCode)]
    private static ClassificationTypeDefinition typeDefinitionForInlineCode;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_title)]
    private static ClassificationTypeDefinition typeDefinitionForTitle;
#pragma warning restore 169
  }





  [Export(typeof(IClassifierProvider))]
  [ContentType("C/C++")]
  internal class FormClassifierProvider : IClassifierProvider
  {
#pragma warning disable 649
    [Import]
    private IClassificationTypeRegistryService classificationRegistry;
#pragma warning restore 649

    public IClassifier GetClassifier(ITextBuffer buffer)
    {
      return buffer.Properties.GetOrCreateSingletonProperty<FormatClassifier>(
        creator: () => new FormatClassifier(this.classificationRegistry));
    }
  }



  internal class FormatClassifier : IClassifier
  {
    internal FormatClassifier(IClassificationTypeRegistryService registry)
    {
      mFormater = new CommentFormatter();

      int numFormats = Enum.GetNames(typeof(FormatTypes)).Length;
      mFormatTypeToClassificationType = new IClassificationType[numFormats];
      mFormatTypeToClassificationType[(uint)FormatTypes.NormalKeyword] = registry.GetClassificationType(IDs.ID_normalKeyword);
      mFormatTypeToClassificationType[(uint)FormatTypes.Warning] = registry.GetClassificationType(IDs.ID_warningKeyword);
      mFormatTypeToClassificationType[(uint)FormatTypes.Note] = registry.GetClassificationType(IDs.ID_noteKeyword);
      mFormatTypeToClassificationType[(uint)FormatTypes.Parameter] = registry.GetClassificationType(IDs.ID_parameter);
      mFormatTypeToClassificationType[(uint)FormatTypes.EmphasisMinor] = registry.GetClassificationType(IDs.ID_emphasisMinor);
      mFormatTypeToClassificationType[(uint)FormatTypes.EmphasisMajor] = registry.GetClassificationType(IDs.ID_emphasisMajor);
      mFormatTypeToClassificationType[(uint)FormatTypes.Strikethrough] = registry.GetClassificationType(IDs.ID_strikethrough);
      mFormatTypeToClassificationType[(uint)FormatTypes.InlineCode] = registry.GetClassificationType(IDs.ID_inlineCode);
      mFormatTypeToClassificationType[(uint)FormatTypes.Title] = registry.GetClassificationType(IDs.ID_title);

      foreach (IClassificationType classificationType in mFormatTypeToClassificationType) {
        Debug.Assert(classificationType != null);
      }
    }

#pragma warning disable 67
    // TODO: We need to call this with a certain span S if we decide that span S should be re-classified.
    // E.g. might need to call this from listening to TextEditor-Changed events, and somewhere the opening of
    // a comment is inserted, and thus all following lines need to be re-classified.
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67


    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan spanToCheck)
    {      
      string codeText = spanToCheck.GetText();
      var fragmentsToFormat = mFormater.FormatText(codeText);

      var result = new List<ClassificationSpan>();
      foreach (FormattedFragment fragment in fragmentsToFormat) {
        IClassificationType classificationType = mFormatTypeToClassificationType[(uint)fragment.Type];

        var spanToFormat = new Span(spanToCheck.Start + fragment.StartIndex, fragment.Length);
        result.Add(new ClassificationSpan(new SnapshotSpan(spanToCheck.Snapshot, spanToFormat), classificationType));
      }

      return result;
    }


    private readonly CommentFormatter mFormater;
    private readonly IClassificationType[] mFormatTypeToClassificationType;
  }

}
