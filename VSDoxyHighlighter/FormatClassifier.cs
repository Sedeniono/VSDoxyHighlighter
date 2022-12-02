using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Tagging;

namespace VSDoxyHighlighter
{
  // Identifiers for the classifications. E.g., Visual Studio uses these strings as keys
  // to store the classification's configuration in the registry.
  public static class IDs
  {
    public const string ID_command = "DoxyTest3Command";
    public const string ID_parameter1 = "DoxyTest3Parameter1";
    public const string ID_parameter2 = "DoxyTest3Parameter2";
    public const string ID_title = "DoxyTest3Title";
    public const string ID_warningKeyword = "DoxyTest3Warning";
    public const string ID_noteKeyword = "DoxyTest3Note";
    public const string ID_emphasisMinor = "DoxyTest3EmphasisMinor";
    public const string ID_emphasisMajor = "DoxyTest3EmphasisMajor";
    public const string ID_strikethrough = "DoxyTest3Strikethrough";
    public const string ID_inlineCode = "DoxyTest3InlineCode";
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
    [Name(IDs.ID_parameter1)]
    private static ClassificationTypeDefinition typeDefinitionForParameter1;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(IDs.ID_parameter2)]
    private static ClassificationTypeDefinition typeDefinitionForParameter2;

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
        creator: () => new FormatClassifier(this.classificationRegistry, buffer));
    }
  }



  internal class FormatClassifier : IClassifier
  {
    internal FormatClassifier(IClassificationTypeRegistryService registry, ITextBuffer textBuffer)
    {
      mTextBuffer = textBuffer;
      mFormater = new CommentFormatter();

      int numFormats = Enum.GetNames(typeof(FormatType)).Length;
      mFormatTypeToClassificationType = new IClassificationType[numFormats];
      mFormatTypeToClassificationType[(uint)FormatType.Command] = registry.GetClassificationType(IDs.ID_command);
      mFormatTypeToClassificationType[(uint)FormatType.Parameter1] = registry.GetClassificationType(IDs.ID_parameter1);
      mFormatTypeToClassificationType[(uint)FormatType.Parameter2] = registry.GetClassificationType(IDs.ID_parameter2);
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
    /// 
    /// The function is typically called with individual lines.
    /// </summary>
    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan originalSpanToCheck)
    {
      ITextSnapshot textSnapshot = originalSpanToCheck.Snapshot;

      List<Span> commentSpans = DecomposeSpanIntoComments(originalSpanToCheck);

      var result = new List<ClassificationSpan>();
      foreach (Span spanToCheck in commentSpans) {
        string codeText = textSnapshot.GetText(spanToCheck);

        // Scan the given text for keywords and get the proper formatting for it.
        var fragmentsToFormat = mFormater.FormatText(codeText);

        // Convert the list of fragments that should be formatted to Visual Studio types.
        foreach (FormattedFragment fragment in fragmentsToFormat) {
          IClassificationType classificationType = mFormatTypeToClassificationType[(uint)fragment.Type];
          var spanToFormat = new Span(spanToCheck.Start + fragment.StartIndex, fragment.Length);
          result.Add(new ClassificationSpan(new SnapshotSpan(textSnapshot, spanToFormat), classificationType));
        }
      }

      return result;
    }


    /// <summary>
    /// Decomposes the given snapshot spans such that only a list of spans is returned that are all comments.
    /// I.e. it filters out all text in the given span that is NOT within a comment, and returns the remaining
    /// parts as list.
    /// </summary>
    List<Span> DecomposeSpanIntoComments(SnapshotSpan spanToCheck)
    {
      var result = new List<Span>();

      // The task is to somehow figure out which parts of the given text represents a comment and which does not.
      // This is something that is absolutely non-trivial in C++:
      //  - Just looking at single lines is by far not sufficient because of multiline comments (/* ... */) and also
      //    because of line continuation (i.e. a "\" at the end of e.g. a normal C++ "//"-style comment causes the
      //    comment to continue in the next line).
      //  - Even a single line might contain multiple independent comments: /* comment */ code /* comment */ code
      //  - In case of multiline comments, simply scanning upwards to the next "/*" to find whether some toke is in
      //    a comment is insufficient because of strings and "//" style comment. Detecting strings by itself is
      //    also highly non-trivial due to similar reasons (multiline strings, raw strings, line continuation via "\").
      //  - A "/*" might not start a comment if there is another "/*" before without a corresponding "*/".
      //    I.e. in "/* foo1 /* foo2 */" the "/*" after "foo1" does not start the comment.
      //  - The code should be fast. Due to the global character of the multiline comments, some sort of caching needs
      //    to be applied. The cache needs to be updated whenever some text changes, but the update should be as local
      //    as possible for performance reasons. Yet, if the user types e.g. "/*", potentially everything afterwards
      //    needs to be re-classified as comment (or not).
      //
      // ==> We do not attempt to implement this. Especially considering that Visual Studio itself must somewhere
      //     somehow already have solved this task. The "somewhere" is in the tagger named "Microsoft.VisualC.CppColorer".
      //     Therefore, we get a reference to that tagger, and ask it to decompose the given span for us. We only keep
      //     those spans that were classified as tokens. This is the idea from https://stackoverflow.com/q/19060596/3740047
      //     Note that all of this is a hack since the VS tagger is not really exposed via a proper API. 
      //     An alternative might be to use the IClassifierAggregatorService (https://stackoverflow.com/a/29311144/3740047).
      //     I have not really tried it, but I fear that calling GetClassifier() calls our code, and thus we might end up with
      //     an infinite recursion. Of course, there would be ways to bypass this problem (if it really does occur). But
      //     the approach with the dedicated tagger seems favorable since it limits the request to only that specific tagger,
      //     and does not involve all the other existing classifiers.
      //
      // ==> There are basically two disadvantages with this approach: First, we cannot really write automated tests for it
      //     because we would need to have a running Visual Studio instance. Second, it is a hack and thus might break without
      //     warning in a future Visual Studio. But the alternative to re-implement the classification logic seems even more
      //     ridiculous.

      var defaultTagger = FindDefaultVSCppTagger();
      if (defaultTagger != null) {
        var defaultTags = defaultTagger.GetTags(new NormalizedSnapshotSpanCollection(spanToCheck.Snapshot, spanToCheck.Span));
        foreach (var tag in defaultTags) {
          string classification = tag.Tag.ClassificationType.Classification;
          // Visual Studio currently knows two different comment types: "comment" and "XML Doc Comment".
          if (classification.ToUpper() == "COMMENT" || classification.ToUpper() == "XML DOC COMMENT") {
            result.Add(tag.Span);
          }
        }
      }
      else {
        // Mh, no tagger found? Maybe Microsoft changed their tagger name?
        result.Add(spanToCheck.Span);
      }

      return result;
    }


    private ITagger<IClassificationTag> FindDefaultVSCppTagger()
    {
      if (mDefaultCppTagger == null) {
        string nameOfDefaultCppTager = "Microsoft.VisualC.CppColorer".ToUpper();
        foreach (var kvp in mTextBuffer.Properties.PropertyList) {
          if (kvp.Value is ITagger<IClassificationTag> casted) {
            if (kvp.Key.ToString().ToUpper() == nameOfDefaultCppTager) {
              mDefaultCppTagger = casted;
              break;
            }
          }
        }
      }

      return mDefaultCppTagger;
    }


    private readonly ITextBuffer mTextBuffer;
    private readonly CommentFormatter mFormater;
    private readonly IClassificationType[] mFormatTypeToClassificationType;

    private ITagger<IClassificationTag> mDefaultCppTagger = null;
  }

}
