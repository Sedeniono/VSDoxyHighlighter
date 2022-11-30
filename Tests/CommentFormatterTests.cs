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

      public FormatType Type { get; private set; }

      public FormattedFragmentText(string text, FormatType type)
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
        new FormattedFragment(8, 6, FormatType.NormalKeyword), // @brief
        new FormattedFragment(47, 8, FormatType.NormalKeyword), // @details
        new FormattedFragment(88, 10, FormatType.NormalKeyword), // @param[in]
        new FormattedFragment(99, 7, FormatType.Parameter), // inParam of the @param[in]
        new FormattedFragment(139, 8, FormatType.Warning), // @warning
        new FormattedFragment(180, 5, FormatType.Note), // @note

        new FormattedFragment(221, 7, FormatType.NormalKeyword), // \tparam
        new FormattedFragment(229, 13, FormatType.Parameter), // templateParam of \tparam
        new FormattedFragment(292, 11, FormatType.NormalKeyword), // \param[out]
        new FormattedFragment(304, 8, FormatType.Parameter), // outParam of the \param[out]
        new FormattedFragment(357, 8, FormatType.NormalKeyword), // \returns
        new FormattedFragment(396, 7, FormatType.NormalKeyword), // \return
        new FormattedFragment(433, 4, FormatType.NormalKeyword), // \see

        new FormattedFragment(490, 6, FormatType.NormalKeyword), // @brief after someCode
        new FormattedFragment(557, 4, FormatType.NormalKeyword), // @ref
        new FormattedFragment(562, 7, FormatType.Parameter), // SomeRef of @ref
        new FormattedFragment(574, 2, FormatType.NormalKeyword), // \p
        new FormattedFragment(577, 5, FormatType.Parameter), // Param of \p

        new FormattedFragment(617, 6, FormatType.NormalKeyword), // @brief after /*!
        new FormattedFragment(645, 6, FormatType.NormalKeyword), // @brief after /**
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
        new FormattedFragment(6, 6, FormatType.NormalKeyword), // @brief
        new FormattedFragment(46, 10, FormatType.NormalKeyword), // @param[in]
        new FormattedFragment(57, 7, FormatType.Parameter), // inParam of @param[in]
        new FormattedFragment(99, 8, FormatType.Warning), // @warning
        new FormattedFragment(151, 5, FormatType.Note), // \note
        new FormattedFragment(192, 7, FormatType.NormalKeyword), // \tparam
        new FormattedFragment(200, 13, FormatType.Parameter), // templateParam of \tparam
        new FormattedFragment(266, 11, FormatType.NormalKeyword), // \param[out]
        new FormattedFragment(278, 8, FormatType.Parameter), // outParam of \param[out]

        new FormattedFragment(360, 4, FormatType.NormalKeyword), // @ref
        new FormattedFragment(365, 7, FormatType.Parameter), // SomeRef of @ref
        new FormattedFragment(377, 2, FormatType.NormalKeyword), // \p
        new FormattedFragment(380, 5, FormatType.Parameter), // Param of \p

        new FormattedFragment(415, 6, FormatType.NormalKeyword), // @brief
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
        new Utils.FormattedFragmentText(@"\addtogroup", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("groupNameWithTitle", FormatType.Parameter),
        new Utils.FormattedFragmentText("Some group title", FormatType.Title),

        new Utils.FormattedFragmentText(@"\addtogroup", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("groupNameWithoutTitle", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\callgraph", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\hidecallgraph", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"@callergraph", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\hidecallergraph", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\showrefby", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\hiderefby", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\showrefs", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\hiderefs", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\category", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("inc/class.h", FormatType.Title),

        new Utils.FormattedFragmentText(@"\class", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatType.Title),
        new Utils.FormattedFragmentText(@"\class", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test2", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test3::MemClass", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test4", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test5", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\class", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\class", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\concept", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"concept_name", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\def", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"MAX(x,y)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\def", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\def", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\defgroup", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"IntVariables", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"Global integer variables", FormatType.Title),

        new Utils.FormattedFragmentText(@"\dir", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\enum", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Enum_Test::TEnum", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\example", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"example_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\example{lineno}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\endinternal", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\extends", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Object", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\file", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\fileinfo{file}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\fileinfo{extension}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\fileinfo{filename}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\fileinfo{directory}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\fileinfo{full}", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\lineinfo", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\fn", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"some name\"", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("\"test.h\"", FormatType.Parameter),
        new Utils.FormattedFragmentText("some name", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"\"", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("\"test.h\"", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("\"\"", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"<test.h>", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"<>", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"<>", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\hideinitializer", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\idlexcept", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"exception", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\implements", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"ISomeInterface_", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\ingroup", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Group1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ingroup", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Group1 Group2 Group3", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\interface", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("inc/class.h", FormatType.Title),

        new Utils.FormattedFragmentText(@"\internal", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\mainpage", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"My Personal Index Page", FormatType.Title),

        new Utils.FormattedFragmentText(@"\memberof", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"_some_name", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\name", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"group_", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\namespace", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"nested::space", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\nosubgrouping", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\overload", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"void Overload_Test::drawRect(const Rect &r)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\package", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"PackageName", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\page", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("page1", FormatType.Parameter),
        new Utils.FormattedFragmentText("A documentation page", FormatType.Title),

        new Utils.FormattedFragmentText(@"\private", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\privatesection", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\property", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\protected", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\protectedsection", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\protocol", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"ProtocolName", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"Header.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("inc/Header.h", FormatType.Title),

        new Utils.FormattedFragmentText(@"\public", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\publicsection", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\pure", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\relates", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"String", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\related", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"String", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\relatesalso", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"String", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\relatedalso", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"String", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\showinitializer", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\static", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\typedef", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"unsigned long ulong", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"@struct", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatType.Title),

        new Utils.FormattedFragmentText(@"\union", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatType.Title),

        new Utils.FormattedFragmentText(@"\var", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"unsigned long variable", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\weakgroup", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("groupNameWithTitle", FormatType.Parameter),
        new Utils.FormattedFragmentText("Some group title", FormatType.Title),


        // --- Section indicators ---
        new Utils.FormattedFragmentText(@"\attention", FormatType.Note),
        new Utils.FormattedFragmentText(@"\author", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\authors", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\brief", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\bug", FormatType.Note),

        new Utils.FormattedFragmentText(@"\cond", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\cond", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\copyright", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\date", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\showdate", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"2015-3-14 03:04:15", FormatType.Title),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""2015-3-14 03:04:15""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"Format even empty """"", FormatType.Title),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\deprecated", FormatType.Note),
        new Utils.FormattedFragmentText(@"\details", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\noop", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\raisewarning", FormatType.Warning),
        new Utils.FormattedFragmentText(@"\else", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\elseif", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\endcond", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endif", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\exception", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"std::out_of_range", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\if", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\if", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Cond1", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\ifnot", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\invariant", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\note", FormatType.Note),

        new Utils.FormattedFragmentText(@"\par", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"User defined paragraph:", FormatType.Title),
        new Utils.FormattedFragmentText(@"\par", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\par", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@":  some title", FormatType.Title),

        new Utils.FormattedFragmentText(@"\param[out]", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"dest", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param[in]", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"src", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param[in]", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"n", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param[in,out]", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"p", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"p", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"x,y,z", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\param", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\param", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\parblock", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endparblock", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\tparam", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_param", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\post", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\pre", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\remark", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\remarks", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\result", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\return", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\returns", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\retval", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_value", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\sa", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\see", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\short", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\since", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\test", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\throw", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"std::out_of_range", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\throws", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"someException", FormatType.Parameter),
        new Utils.FormattedFragmentText("@throws", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("std::runtime_error", FormatType.Parameter),
        new Utils.FormattedFragmentText("@ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("someFunc()", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\todo", FormatType.Note),
        new Utils.FormattedFragmentText(@"\version", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\warning", FormatType.Warning),


        // --- Commands to create links ---

        new Utils.FormattedFragmentText(@"\addindex", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\anchor", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_word", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\cite", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_label", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\link", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"link_obj", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\endlink", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endlink", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"subsection1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"subsection2", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"subsection3", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"link_text", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""some text""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"someFunc()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""some text 2""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"link_text3", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""some""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"link_text5", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::Func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::cls::func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class.Func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class.Func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::Func(double,int)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"func(double, int)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::Func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class::Func(int, double, cls::f)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class2", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Class3", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"match", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\ref", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\refitem", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some_name", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\secreflist", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endsecreflist", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\subpage", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"intro", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\subpage", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"advanced", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""Advanced usage""", FormatType.Title),

        new Utils.FormattedFragmentText(@"\tableofcontents", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\section", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"sec", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatType.Title),
        new Utils.FormattedFragmentText(@"\section", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\section", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\subsection", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"sec_2", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\subsubsection", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"sec", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatType.Title),
        new Utils.FormattedFragmentText(@"\paragraph", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"sec", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatType.Title),


        // --- Commands for displaying examples ---

        new Utils.FormattedFragmentText(@"\dontinclude", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dontinclude{lineno}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\include", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\include{lineno}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\include{doc}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""some dir\include_test.cpp""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\includelineno", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\includedoc", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\line", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"example();", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\skip", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"main", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\skipline", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"Include_Test t;", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\until", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"{", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\snippet", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"Adding a resource", FormatType.Title),
        new Utils.FormattedFragmentText(@"\snippet{lineno}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatType.Title),
        new Utils.FormattedFragmentText(@"\snippet{doc}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatType.Title),
        new Utils.FormattedFragmentText(@"\snippetlineno", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatType.Title),
        new Utils.FormattedFragmentText(@"\snippetdoc", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatType.Title),

        new Utils.FormattedFragmentText(@"\verbinclude", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\htmlinclude", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\html.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\htmlinclude[block]", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"html.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\latexinclude", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\tex.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\rtfinclude", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\rtf.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\maninclude", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\man.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\docbookinclude", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\doc.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\xmlinclude", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"some dir\xml.cpp", FormatType.Parameter),


        // --- Commands for visual enhancements ---

        new Utils.FormattedFragmentText(@"\a", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatType.EmphasisMinor),
        new Utils.FormattedFragmentText(@"@a", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"y::p", FormatType.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\b", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatType.EmphasisMajor),
        new Utils.FormattedFragmentText(@"@b", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"y::p", FormatType.EmphasisMajor),

        new Utils.FormattedFragmentText(@"\c", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"@c", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"y::p", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\p", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"@p", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"y::p", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\p", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"::thing", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\p", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\p", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\p", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"thing", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\arg", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\li", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\code{.py}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endcode", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\code{.cpp}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endcode", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\code{.unparsed}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endcode", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\code", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endcode", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"@copydoc", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"MyClass::myfunction(type1,type2)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"@copydoc", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"MyClass::myfunction()", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\brief", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\copybrief", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"foo()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\details", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\copydetails", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"foo()", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\docbookonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\enddocbookonly", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\dot", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"width=2\textwidth", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"height=\textwidth", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\dot", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\dot", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\enddot", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\emoji", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@":smile:", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\emoji", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"left_luggage", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"@msc", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\msc", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\endmsc", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\startuml", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"@startuml{myimage.png}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""Image Caption""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"@startuml{json, myimage.png}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""Image Caption""", FormatType.Title),
        new Utils.FormattedFragmentText(@"@startuml{json}", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"@enduml", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\dotfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"filename", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""file name""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""foo  test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"filename", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""file name""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""file name""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"filename", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\mscfile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"file_name.msc", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\diafile", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"""path\with space\file_name.dia""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\e", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"really", FormatType.EmphasisMinor),
        new Utils.FormattedFragmentText(@"\em", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"x", FormatType.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\htmlonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\htmlonly[block]", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endhtmlonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\latexonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endlatexonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\manonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endmanonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\rtfonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endrtfonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\verbatim", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endverbatim", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\xmlonly", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\endxmlonly", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\f$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f(", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f)", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f[", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f]", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\f{eqnarray*}{", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f{eqnarray*}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\f}", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"html", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"html", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"application.jpg", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"latex", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"application.eps", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""My application""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=10cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"docbook", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""file name.eps""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image{inline,anchor:id}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"rtf", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""path with space/name.rtf""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image{inline}", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"xml", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"file.xml", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\n", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\n", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\n", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\n", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\@", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\~", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~english", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~english", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~dutch", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~german", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\~", FormatType.NormalKeyword),

        new Utils.FormattedFragmentText(@"\&", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\$", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\#", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\<", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\>", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\%", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\.", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\=", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\::", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\|", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\--", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"\---", FormatType.NormalKeyword),
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
        new FormattedFragment(9, 8, FormatType.EmphasisMinor),
        new FormattedFragment(33, 8, FormatType.EmphasisMinor),
        new FormattedFragment(52, 13, FormatType.EmphasisMinor),
        new FormattedFragment(76, 16, FormatType.EmphasisMinor),
        new FormattedFragment(110, 13, FormatType.EmphasisMinor),
        new FormattedFragment(140, 8, FormatType.EmphasisMinor),
        new FormattedFragment(155, 8, FormatType.EmphasisMinor),
        new FormattedFragment(178, 10, FormatType.EmphasisMinor),
        new FormattedFragment(207, 15, FormatType.EmphasisMinor),
        new FormattedFragment(230, 11, FormatType.EmphasisMinor),
      };
    }


    [TestMethod()]
    public void DoubleStarShouldFormatBold()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_DoubleStar.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(FormatType.EmphasisMajor);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void DoubleUnderscoreShouldFormatBold()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_DoubleUnderscore.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(FormatType.EmphasisMajor);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void DoubleTildeShouldFormatStrikethrough()
    {
      var formatter = new CommentFormatter();
      var actualFragments = formatter.FormatText(
        Utils.ReadTestInputFromFile("Markdown_DoubleTilde.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(FormatType.Strikethrough);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    private List<FormattedFragment> GetExpectationsForBoldOrStrikethrough(FormatType expectedFormat)
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
        new FormattedFragment(4, 13, FormatType.InlineCode),
        new FormattedFragment(30, 8, FormatType.InlineCode),
        new FormattedFragment(48, 13, FormatType.InlineCode),
        new FormattedFragment(72, 6, FormatType.InlineCode),
        new FormattedFragment(94, 2, FormatType.InlineCode),
        new FormattedFragment(114, 7, FormatType.InlineCode),
        new FormattedFragment(121, 7, FormatType.InlineCode),
        new FormattedFragment(134, 26, FormatType.InlineCode),
        new FormattedFragment(167, 45, FormatType.InlineCode),
      };

      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void UnicodeParametersShouldWork()
    {
      var input = Utils.ReadTestInputFromFile("UnicodeParametersUTF8.cpp");
      var actualFragments = new CommentFormatter().FormatText(input);

      var expectedTextFragments = new List<Utils.FormattedFragmentText>() {
        new Utils.FormattedFragmentText("@param", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText("t\U0001F600t", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\image", FormatType.NormalKeyword),
        new Utils.FormattedFragmentText(@"latex", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"file \U0001F600 name.eps\"", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"test\U0001F600\"", FormatType.Title),

        new Utils.FormattedFragmentText("**te\U0001F600st**", FormatType.EmphasisMajor),
      };

      var actualTextFragments = Utils.ConvertToTextFragments(input, actualFragments);
      CollectionAssert.AreEquivalent(expectedTextFragments, actualTextFragments);
    }
  }
}
