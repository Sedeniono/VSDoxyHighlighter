using Microsoft.VisualStudio.TestTools.UnitTesting;
using VSDoxyHighlighter;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;


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
  }


  [TestClass()]
  public class CommentFormatterTests
  {
    [TestMethod()]
    public void EmptyStringShouldCauseNoFormatting()
    {
      var formatter = new CommentFormatter();
      List<FormattedFragment> actualFagments = formatter.FormatText("");
      Assert.IsNotNull(actualFagments);
      Assert.AreEqual(0, actualFagments.Count);
    }


    [TestMethod()]
    public void BasicCStyleCommentsShouldBeFormatted()
    {
      var formatter = new CommentFormatter();
      List<FormattedFragment> actualFagments = formatter.FormatText(
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

      CollectionAssert.AreEquivalent(expectedFragments, actualFagments);
    }


    [TestMethod()]
    public void BasicCppStyleCommentsShouldBeFormatted()
    {
      var formatter = new CommentFormatter();
      List<FormattedFragment> actualFagments = formatter.FormatText(
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

      CollectionAssert.AreEquivalent(expectedFragments, actualFagments);
    }


    [TestMethod()]
    public void CasesWhereNothingShouldBeFormatted()
    {
      var formatter = new CommentFormatter();
      List<FormattedFragment> actualFagments = formatter.FormatText(
        Utils.ReadTestInputFromFile("NothingToFormat.cpp"));

      Assert.AreEqual(0, actualFagments.Count);
    }
  }
}