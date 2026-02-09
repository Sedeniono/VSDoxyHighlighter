using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace VSDoxyHighlighter.Tests
{
  internal class Utils
  {
    public static string ReadTestInputFromFile(string filenameWithoutPath)
    {
      string fileContent = File.ReadAllText("InputFiles\\" + filenameWithoutPath);
      if (string.IsNullOrEmpty(fileContent)) {
        throw new ArgumentException("Input file '" + filenameWithoutPath + "' could not be read.");
      }
      return fileContent;
    }


    /// <summary>
    /// Used for tests where we do not check the position.
    /// </summary>
    [DebuggerDisplay("Text={Text}, Classification={Classification}")]
    public struct FormattedFragmentText
    {
      public string Text { get; private set; }

      public ClassificationEnum Classification { get; private set; }

      public FormattedFragmentText(string text, ClassificationEnum classification)
      {
        Debug.Assert(text.Length > 0);
        Text = text;
        Classification = classification;
      }
    }


    public static List<FormattedFragmentText> ConvertToTextFragments(string text, IEnumerable<FormattedFragmentGroup> fragmentGroups)
    {
      var result = new List<FormattedFragmentText>();
      foreach (FormattedFragmentGroup group in fragmentGroups) {
        foreach (FormattedFragment fragment in group.Fragments) {
          Assert.IsTrue(fragment.Length > 0);
          Assert.IsTrue(fragment.EndIndex < text.Length);
          string str = text.Substring(fragment.StartIndex, fragment.Length);
          result.Add(new FormattedFragmentText(str, fragment.Classification));
        }
      }
      return result;
    }


    public static List<FormattedFragment> ToFlatFragmentList(IEnumerable<FormattedFragmentGroup> fragmentGroups)
    {
      var result = new List<FormattedFragment>();
      foreach (FormattedFragmentGroup group in fragmentGroups) {
        result.AddRange(group.Fragments);
      }
      return result;
    }


    public static void WriteFragmentsToFile(string filename, List<FormattedFragmentText> fragments)
    {
      using (StreamWriter writer = new StreamWriter(filename)) {
        foreach (Utils.FormattedFragmentText fragment in fragments) {
          writer.WriteLine($"Text={fragment.Text}, Type={fragment.Classification}");
        }
      }
    }


    public static CommentParser CreateDefaultCommentParser()
    {
      return new CommentParser(new DoxygenCommands(new GeneralOptionsFake()));
    }
  }
}
