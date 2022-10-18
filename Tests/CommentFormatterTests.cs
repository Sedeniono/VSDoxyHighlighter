using Microsoft.VisualStudio.TestTools.UnitTesting;
using VSDoxyHighlighter;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using static System.Collections.Specialized.BitVector32;

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
    [DebuggerDisplay("Text={Text}, Type={Type}")]
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

        new FormattedFragment(617, 6, FormatTypes.NormalKeyword), // @brief after /*!
        new FormattedFragment(645, 6, FormatTypes.NormalKeyword), // @brief after /**
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
        // --- Structural indicators --- 
        new Utils.FormattedFragmentText("@throws", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("std::runtime_error", FormatTypes.Parameter),

        new Utils.FormattedFragmentText("@ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("someFunc()", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\addtogroup", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("groupNameWithTitle", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("Some group title", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\addtogroup", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("groupNameWithoutTitle", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\callgraph", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\hidecallgraph", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"@callergraph", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\hidecallergraph", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\showrefby", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\hiderefby", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\showrefs", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\hiderefs", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\category", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("inc/class.h", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\class", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\class", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test2", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test3", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\concept", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"concept_name", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\def", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"MAX(x,y)", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\defgroup", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"IntVariables", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"Global integer variables", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\dir", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\enum", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Enum_Test::TEnum", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\example", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"example_test.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\example{lineno}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\endinternal", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\extends", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Object", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\file", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\fileinfo{file}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\fileinfo{extension}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\fileinfo{filename}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\fileinfo{directory}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\fileinfo{full}", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\lineinfo", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\fn", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("\"some name\"", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("\"test.h\"", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("some name", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("\"\"", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("\"test.h\"", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("\"\"", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"<test.h>", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"<>", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"<>", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\hideinitializer", FormatTypes.NormalKeyword),
        
        new Utils.FormattedFragmentText(@"\idlexcept", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"exception", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\implements", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"ISomeInterface_", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\ingroup", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Group1", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ingroup", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Group1 Group2 Group3", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\interface", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("inc/class.h", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\internal", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\mainpage", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"My Personal Index Page", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\memberof", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"_some_name", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\name", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"group_", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\namespace", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"nested::space", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\nosubgrouping", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\overload", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"void Overload_Test::drawRect(const Rect &r)", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\package", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"PackageName", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\page", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("page1", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("A documentation page", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\private", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\privatesection", FormatTypes.NormalKeyword),
        
        new Utils.FormattedFragmentText(@"\property", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\protected", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\protectedsection", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\protocol", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"ProtocolName", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"Header.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("inc/Header.h", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\public", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\publicsection", FormatTypes.NormalKeyword),
        
        new Utils.FormattedFragmentText(@"\pure", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\relates", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"String", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\related", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"String", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\relatesalso", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"String", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\relatedalso", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"String", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\showinitializer", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\static", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\typedef", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"unsigned long ulong", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"@struct", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\union", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\var", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"unsigned long variable", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\weakgroup", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("groupNameWithTitle", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("Some group title", FormatTypes.Title),

        // --- Section indicators ---
        new Utils.FormattedFragmentText(@"\attention", FormatTypes.Note),
        new Utils.FormattedFragmentText(@"\author", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\authors", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\bug", FormatTypes.Note),

        new Utils.FormattedFragmentText(@"\cond", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\cond", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\copyright", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\date", FormatTypes.NormalKeyword),
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
        new FormattedFragment(207, 15, FormatTypes.EmphasisMinor),
        new FormattedFragment(230, 11, FormatTypes.EmphasisMinor),
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
        new FormattedFragment(207, 15, FormatTypes.EmphasisMajor),
        new FormattedFragment(230, 13, FormatTypes.EmphasisMajor),
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