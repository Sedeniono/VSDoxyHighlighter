using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;


namespace VSDoxyHighlighter
{
  // Identifiers for the classifications. E.g., Visual Studio uses these strings as keys
  // to store the classification's configuration in the registry.
  public static class IDs
  {
    public const string ID_command = "DoxyTestCommand";
    public const string ID_parameter = "DoxyTestParameter";
    public const string ID_title = "DoxyTestTitle";
    public const string ID_warningKeyword = "DoxyTestWarning";
    public const string ID_noteKeyword = "DoxyTestNote";
    public const string ID_emphasisMinor = "DoxyTestEmphasisMinor";
    public const string ID_emphasisMajor = "DoxyTestEmphasisMajor";
    public const string ID_strikethrough = "DoxyTestStrikethrough";
    public const string ID_inlineCode = "DoxyTestInlineCode";
  }




  internal static class TestClassifierClassificationDefinition
  {
#pragma warning disable 169
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_command)]
    private static ClassificationTypeDefinition typeDefinitionForCommand;

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

      int numFormats = Enum.GetNames(typeof(FormatType)).Length;
      mFormatTypeToClassificationType = new IClassificationType[numFormats];
      mFormatTypeToClassificationType[(uint)FormatType.Command] = registry.GetClassificationType(IDs.ID_command);
      mFormatTypeToClassificationType[(uint)FormatType.Parameter] = registry.GetClassificationType(IDs.ID_parameter);
      mFormatTypeToClassificationType[(uint)FormatType.Title] = registry.GetClassificationType(IDs.ID_title);
      mFormatTypeToClassificationType[(uint)FormatType.Warning] = registry.GetClassificationType(IDs.ID_warningKeyword);
      mFormatTypeToClassificationType[(uint)FormatType.Note] = registry.GetClassificationType(IDs.ID_noteKeyword);
      mFormatTypeToClassificationType[(uint)FormatType.EmphasisMinor] = registry.GetClassificationType(IDs.ID_emphasisMinor);
      mFormatTypeToClassificationType[(uint)FormatType.EmphasisMajor] = registry.GetClassificationType(IDs.ID_emphasisMajor);
      mFormatTypeToClassificationType[(uint)FormatType.Strikethrough] = registry.GetClassificationType(IDs.ID_strikethrough);
      mFormatTypeToClassificationType[(uint)FormatType.InlineCode] = registry.GetClassificationType(IDs.ID_inlineCode);

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


    /// <summary>
    /// Called by Visual Studio when the given text span needs to be classified (i.e. formatted).
    /// Thus, this function searches for words to which apply syntax highlighting, and for each one 
    /// found returns a ClassificationSpan.
    /// </summary>
    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan spanToCheck)
    {      
      string codeText = spanToCheck.GetText();

      // Scan the given text for keywords and get the proper formatting for it.
      var fragmentsToFormat = mFormater.FormatText(codeText);

      // Convert the list of fragments that should be formatted to Visual Studio types.
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
