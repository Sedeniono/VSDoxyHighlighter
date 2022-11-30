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
      foreach (FormattedFragment fragment in fragments) {
        Assert.IsTrue(fragment.Length > 0);
        Assert.IsTrue(fragment.EndIndex < text.Length);
        string str = text.Substring(fragment.StartIndex, fragment.Length);
        result.Add(new FormattedFragmentText(str, fragment.Type));
      }
      return result;
    }


    public static void WriteFragmentsToFile(string filename, List<FormattedFragmentText> fragments)
    {
      using (StreamWriter writer = new StreamWriter(filename)) {
        foreach (Utils.FormattedFragmentText fragment in fragments) {
          writer.WriteLine($"Text={fragment.Text}, Type={fragment.Type}");
        }
      }
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
        new Utils.FormattedFragmentText(@"\def", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\def", FormatTypes.NormalKeyword),

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
        new Utils.FormattedFragmentText(@"\brief", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\bug", FormatTypes.Note),

        new Utils.FormattedFragmentText(@"\cond", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\cond", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\copyright", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\date", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\showdate", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"2015-3-14 03:04:15", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\showdate", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""2015-3-14 03:04:15""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\showdate", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\deprecated", FormatTypes.Note),
        new Utils.FormattedFragmentText(@"\details", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\noop", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\raisewarning", FormatTypes.Warning),
        new Utils.FormattedFragmentText(@"\else", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\elseif", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\endcond", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endif", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\exception", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"std::out_of_range", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\if", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\if", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Cond1", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\ifnot", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\invariant", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\note", FormatTypes.Note),

        new Utils.FormattedFragmentText(@"\par", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"User defined paragraph:", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\par", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\par", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@":  some title", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\param[out]", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"dest", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\param[in]", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"src", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\param[in]", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"n", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\param[in,out]", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"p", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"p", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"x,y,z", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\param", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\param", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\parblock", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endparblock", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\tparam", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_param", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\post", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\pre", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\remark", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\remarks", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\result", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\return", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\returns", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\retval", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_value", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\sa", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\see", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\short", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\since", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\test", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\throw", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"std::out_of_range", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\throws", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"someException", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("@throws", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("std::runtime_error", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("@ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("someFunc()", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\todo", FormatTypes.Note),
        new Utils.FormattedFragmentText(@"\version", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\warning", FormatTypes.Warning),


        // --- Commands to create links ---

        new Utils.FormattedFragmentText(@"\addindex", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\anchor", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_word", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\cite", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_label", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\link", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"link_obj", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\endlink", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endlink", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"subsection1", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"subsection2", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"subsection3", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"link_text", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""some text""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"someFunc()", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""some text 2""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"link_text3", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""some""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"link_text5", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::Func()", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::cls::func()", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class.Func()", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class.Func()", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::Func(double,int)", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"func(double, int)", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::Func()", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::Func(int, double, cls::f)", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"func()", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\refitem", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_name", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\secreflist", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endsecreflist", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\subpage", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"intro", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\subpage", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"advanced", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""Advanced usage""", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\tableofcontents", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\section", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"sec", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\subsection", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"sec_2", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\subsubsection", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"sec", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\paragraph", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"sec", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatTypes.Title),


        // --- Commands for displaying examples ---

        new Utils.FormattedFragmentText(@"\dontinclude", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dontinclude{lineno}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\include", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\include{lineno}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\include{doc}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""some dir\include_test.cpp""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\includelineno", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\includedoc", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\line", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"example();", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\skip", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"main", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\skipline", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"Include_Test t;", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\until", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"{", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\snippet", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"Adding a resource", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\snippet{lineno}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\snippet{doc}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"example.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\snippetlineno", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\snippetdoc", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"example.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatTypes.Title),

        new Utils.FormattedFragmentText(@"\verbinclude", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\htmlinclude", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\html.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\htmlinclude[block]", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"html.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\latexinclude", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\tex.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\rtfinclude", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\rtf.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\maninclude", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\man.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\docbookinclude", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\doc.cpp", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\xmlinclude", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\xml.cpp", FormatTypes.Parameter),


        // --- Commands for visual enhancements ---

        new Utils.FormattedFragmentText(@"\a", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatTypes.EmphasisMinor),
        new Utils.FormattedFragmentText(@"@a", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"y::p", FormatTypes.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\b", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatTypes.EmphasisMajor),
        new Utils.FormattedFragmentText(@"@b", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"y::p", FormatTypes.EmphasisMajor),

        new Utils.FormattedFragmentText(@"\c", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"@c", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"y::p", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\p", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"@p", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"y::p", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\arg", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\li", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\code{.py}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endcode", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\code{.cpp}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endcode", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\code{.unparsed}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endcode", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\code", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endcode", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"@copydoc", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"MyClass::myfunction(type1,type2)", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"@copydoc", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"MyClass::myfunction()", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\brief", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\copybrief", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"foo()", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\details", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\copydetails", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"foo()", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\docbookonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\enddocbookonly", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\dot", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"width=2\textwidth", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"height=\textwidth", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\dot", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\dot", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\enddot", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\emoji", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@":smile:", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\emoji", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"left_luggage", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"@msc", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\msc", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\endmsc", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\startuml", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"@startuml{myimage.png}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""Image Caption""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"@startuml{json, myimage.png}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""Image Caption""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"@startuml{json}", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"@enduml", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\dotfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"filename", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""file name""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""foo  test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"filename", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""file name""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""file name""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\dotfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"filename", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"\dotfile", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\mscfile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"file_name.msc", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""test""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\diafile", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"""path\with space\file_name.dia""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\e", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"really", FormatTypes.EmphasisMinor),
        new Utils.FormattedFragmentText(@"\em", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatTypes.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\htmlonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\htmlonly[block]", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endhtmlonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\latexonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endlatexonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\manonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endmanonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\rtfonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endrtfonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\verbatim", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endverbatim", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\xmlonly", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endxmlonly", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\f$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f(", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f)", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f[", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f]", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\f{eqnarray*}{", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f{eqnarray*}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f}", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\image", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"html", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"html", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"application.jpg", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"latex", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"application.eps", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""My application""", FormatTypes.Title),
        new Utils.FormattedFragmentText(@"width=10cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"docbook", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""file name.eps""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"width=200cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\image{inline,anchor:id}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"rtf", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"""path with space/name.rtf""", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"\image{inline}", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"xml", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"file.xml", FormatTypes.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\n", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\n", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\n", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\n", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\@", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\~", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~english", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~english", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~dutch", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~german", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~", FormatTypes.NormalKeyword),

        new Utils.FormattedFragmentText(@"\&", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\$", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\#", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\<", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\>", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\%", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\.", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\=", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\::", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\|", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\--", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"\---", FormatTypes.NormalKeyword),
      };

      var actualTextFragments = Utils.ConvertToTextFragments(input, actualFragments);

      // Write fragments to file for easy checking of test failures.
      Utils.WriteFragmentsToFile("VariousKeywords_Expected.txt", expectedTextFragments);
      Utils.WriteFragmentsToFile("VariousKeywords_Actual.txt", actualTextFragments);

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

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(FormatTypes.EmphasisMajor);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void DoubleUnderscoreShouldFormatBold()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_DoubleUnderscore.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(FormatTypes.EmphasisMajor);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void DoubleTildeShouldFormatStrikethrough()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_DoubleTilde.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(FormatTypes.Strikethrough);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    private List<FormattedFragment> GetExpectationsForBoldOrStrikethrough(FormatTypes expectedFormat)
    {
      return new List<FormattedFragment>() {
        new FormattedFragment(9, 8, expectedFormat),
        new FormattedFragment(33, 8, expectedFormat),
        new FormattedFragment(52, 13, expectedFormat),
        new FormattedFragment(76, 18, expectedFormat),
        new FormattedFragment(110, 14, expectedFormat),
        new FormattedFragment(142, 8, expectedFormat),
        new FormattedFragment(157, 8, expectedFormat),
        new FormattedFragment(180, 10, expectedFormat),
        new FormattedFragment(207, 15, expectedFormat),
        new FormattedFragment(230, 13, expectedFormat),
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


    [TestMethod()]
    public void UnicodeParametersShouldWork()
    {
      var input = Utils.ReadTestInputFromFile("UnicodeParametersUTF8.cpp");
      var actualFragments = new CommentFormatter().FormatText(input);

      var expectedTextFragments = new List<Utils.FormattedFragmentText>() {
        new Utils.FormattedFragmentText("@param", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText("t\U0001F600t", FormatTypes.Parameter),

        new Utils.FormattedFragmentText(@"\image", FormatTypes.NormalKeyword),
        new Utils.FormattedFragmentText(@"latex", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("\"file \U0001F600 name.eps\"", FormatTypes.Parameter),
        new Utils.FormattedFragmentText("\"test\U0001F600\"", FormatTypes.Title),

        new Utils.FormattedFragmentText("**te\U0001F600st**", FormatTypes.EmphasisMajor),
      };

      var actualTextFragments = Utils.ConvertToTextFragments(input, actualFragments);
      CollectionAssert.AreEquivalent(expectedTextFragments, actualTextFragments);
    }
  }
}
