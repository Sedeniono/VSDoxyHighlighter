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
    public const string ID_normalKeyword = "DocCom/NormalTEST";
    public const string ID_warningKeyword = "DocCom/WarningTEST";
    public const string ID_noteKeyword = "DocCom/NoteTEST";
    public const string ID_parameter = "DocCom/ParameterTEST";
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
      List<FormattedFragment> fragmentsToFormat = mFormater.FormatText(codeText);

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
