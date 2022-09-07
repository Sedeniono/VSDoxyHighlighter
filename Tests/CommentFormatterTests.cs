using Microsoft.VisualStudio.TestTools.UnitTesting;
using VSDoxyHighlighter;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace VSDoxyHighlighter.Tests
{
  class Utils
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
    public struct FormattedFragmentText
    {
      public string Text { get; private set; }

      public FormatTypes Type { get; private set; }

      public FormattedFragmentText(string text, FormatTypes type)
      {
        Debug.Assert(text.Length > 0);
        Text = text;
        Type = type;
      }
    }


    public static List<FormattedFragmentText> ConvertToTextFragments(string text, ICollection<FormattedFragment> fragments)
    {
      var result = new List<FormattedFragmentText>();
      foreach (FormattedFragment fragment in fragments)
      {
        Assert.IsTrue(fragment.Length > 0);
        Assert.IsTrue(fragment.EndIndex < text.Length);
        string str = text.Substring(fragment.StartIndex, fragment.Length);
        result.Add(new FormattedFragmentText(str, fragment.Type));
      }
      return result;
    }

  }


  [TestClass()]
  public class CommentFormatterTests
  {
    [TestMethod()]
    public void EmptyStringShouldCauseNoFormatting()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText("");
      Assert.IsNotNull(actualFragments);
      Assert.AreEqual(0, actualFragments.Count);
    }


    [TestMethod()]
    public void BasicCStyleCommentsShouldBeFormatted()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("BasicCStyleFormatting.cpp"));

      var expectedFragments = new List<FormattedFragment>() {
        new FormattedFragment(8, 6, FormatTypes.NormalKeyword), // @brief
        new FormattedFragment(47, 8, FormatTypes.NormalKeyword), // @details
        new FormattedFragment(88, 10, FormatTypes.NormalKeyword), // @param[in]
        new FormattedFragment(99, 7, FormatTypes.Parameter), // inParam of the @param[in]
        new FormattedFragment(139, 8, FormatTypes.Warning), // @warning
        new FormattedFragment(180, 5, FormatTypes.Note), // @note

        new FormattedFragment(221, 7, FormatTypes.NormalKeyword), // \tparam
        new FormattedFragment(229, 13, FormatTypes.Parameter), // templateParam of \tparam
        new FormattedFragment(292, 11, FormatTypes.NormalKeyword), // \param[out]
        new FormattedFragment(304, 8, FormatTypes.Parameter), // outParam of the \param[out]
        new FormattedFragment(357, 8, FormatTypes.NormalKeyword), // \returns
        new FormattedFragment(396, 7, FormatTypes.NormalKeyword), // \return
        new FormattedFragment(433, 4, FormatTypes.NormalKeyword), // \see

        new FormattedFragment(490, 6, FormatTypes.NormalKeyword), // @brief after someCode
        new FormattedFragment(557, 4, FormatTypes.NormalKeyword), // @ref
        new FormattedFragment(562, 7, FormatTypes.Parameter), // SomeRef of @ref
        new FormattedFragment(574, 2, FormatTypes.NormalKeyword), // \p
        new FormattedFragment(577, 5, FormatTypes.Parameter), // Param of \p
      };

      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void BasicCppStyleCommentsShouldBeFormatted()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("BasicCppStyleFormatting.cpp"));

      var expectedFragments = new List<FormattedFragment>() {
        new FormattedFragment(6, 6, FormatTypes.NormalKeyword), // @brief
        new FormattedFragment(46, 10, FormatTypes.NormalKeyword), // @param[in]
        new FormattedFragment(57, 7, FormatTypes.Parameter), // inParam of @param[in]
        new FormattedFragment(99, 8, FormatTypes.Warning), // @warning
        new FormattedFragment(151, 5, FormatTypes.Note), // \note
        new FormattedFragment(192, 7, FormatTypes.NormalKeyword), // \tparam
        new FormattedFragment(200, 13, FormatTypes.Parameter), // templateParam of \tparam
        new FormattedFragment(266, 11, FormatTypes.NormalKeyword), // \param[out]
        new FormattedFragment(278, 8, FormatTypes.Parameter), // outParam of \param[out]

        new FormattedFragment(360, 4, FormatTypes.NormalKeyword), // @ref
        new FormattedFragment(365, 7, FormatTypes.Parameter), // SomeRef of @ref
        new FormattedFragment(377, 2, FormatTypes.NormalKeyword), // \p
        new FormattedFragment(380, 5, FormatTypes.Parameter), // Param of \p

        new FormattedFragment(415, 6, FormatTypes.NormalKeyword), // @brief
      };

      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void VariousKeywordsShouldBeFormatted()
    {
      var input = Utils.ReadTestInputFromFile("VariousKeywords.cpp");
      var actualFragments = new CommentFormatter().FormatText(input);

      var expectedTextFragments = new List<Utils.FormattedFragmentText>() {
        new Utils.FormattedFragmentText("@throws", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("std::runtime_error", FormatTypes.Parameter),

        new Utils.FormattedFragmentText("@ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("someFunc()", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\addtogroup", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("groupNameWithTitle", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("Some group title", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\addtogroup", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("groupNameWithoutTitle", FormatTypes.Parameter),
     };

      var actualTextFragments = Utils.ConvertToTextFragments(input, actualFragments);
      CollectionAssert.AreEquivalent(expectedTextFragments, actualTextFragments);
    }


    [TestMethod()]
    public void CasesWhereNothingShouldBeFormatted()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("NothingToFormat.cpp"));

      Assert.AreEqual(0, actualFragments.Count);
    }


    [TestMethod()]
    public void SingleStarShouldFormatItalic()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_SingleStar.cpp"));

      var expectedFragments = GetExpectationsForItalic();
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void SingleUnderscoreShouldFormatItalic()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_SingleStar.cpp"));

      var expectedFragments = GetExpectationsForItalic();
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    private List<FormattedFragment> GetExpectationsForItalic() 
    {
      return new List<FormattedFragment>() {
        new FormattedFragment(9, 8, FormatTypes.EmphasisMinor),
        new FormattedFragment(33, 8, FormatTypes.EmphasisMinor),
        new FormattedFragment(52, 13, FormatTypes.EmphasisMinor),
        new FormattedFragment(76, 16, FormatTypes.EmphasisMinor),
        new FormattedFragment(110, 13, FormatTypes.EmphasisMinor),
        new FormattedFragment(140, 8, FormatTypes.EmphasisMinor),
        new FormattedFragment(155, 8, FormatTypes.EmphasisMinor),
        new FormattedFragment(178, 10, FormatTypes.EmphasisMinor),
      };
    }


    [TestMethod()]
    public void DoubleStarShouldFormatBold()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_DoubleStar.cpp"));

      var expectedFragments = GetExpectationsForBold();
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void DoubleUnderscoreShouldFormatBold()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_DoubleUnderscore.cpp"));

      var expectedFragments = GetExpectationsForBold();
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    private List<FormattedFragment> GetExpectationsForBold()
    {
      return new List<FormattedFragment>() {
        new FormattedFragment(9, 8, FormatTypes.EmphasisMajor),
        new FormattedFragment(33, 8, FormatTypes.EmphasisMajor),
        new FormattedFragment(52, 13, FormatTypes.EmphasisMajor),
        new FormattedFragment(76, 18, FormatTypes.EmphasisMajor),
        new FormattedFragment(110, 14, FormatTypes.EmphasisMajor),
        new FormattedFragment(142, 8, FormatTypes.EmphasisMajor),
        new FormattedFragment(157, 8, FormatTypes.EmphasisMajor),
        new FormattedFragment(180, 10, FormatTypes.EmphasisMajor),
      };
    }


    [TestMethod()]
    public void InlineCodeShouldBeFormatted()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_InlineCode.cpp"));

      var expectedFragments = new List<FormattedFragment>() {
        new FormattedFragment(4, 13, FormatTypes.InlineCode),
        new FormattedFragment(30, 8, FormatTypes.InlineCode),
        new FormattedFragment(48, 13, FormatTypes.InlineCode),
        new FormattedFragment(72, 6, FormatTypes.InlineCode),
        new FormattedFragment(94, 2, FormatTypes.InlineCode),
        new FormattedFragment(114, 7, FormatTypes.InlineCode),
        new FormattedFragment(121, 7, FormatTypes.InlineCode),
        new FormattedFragment(134, 26, FormatTypes.InlineCode),
        new FormattedFragment(167, 45, FormatTypes.InlineCode),
      };

      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }
  }
}