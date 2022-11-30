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
        new FormattedFragment(8, 6, FormatType.Command), // @brief
        new FormattedFragment(47, 8, FormatType.Command), // @details
        new FormattedFragment(88, 10, FormatType.Command), // @param[in]
        new FormattedFragment(99, 7, FormatType.Parameter), // inParam of the @param[in]
        new FormattedFragment(139, 8, FormatType.Warning), // @warning
        new FormattedFragment(180, 5, FormatType.Note), // @note

        new FormattedFragment(221, 7, FormatType.Command), // \tparam
        new FormattedFragment(229, 13, FormatType.Parameter), // templateParam of \tparam
        new FormattedFragment(292, 11, FormatType.Command), // \param[out]
        new FormattedFragment(304, 8, FormatType.Parameter), // outParam of the \param[out]
        new FormattedFragment(357, 8, FormatType.Command), // \returns
        new FormattedFragment(396, 7, FormatType.Command), // \return
        new FormattedFragment(433, 4, FormatType.Command), // \see

        new FormattedFragment(490, 6, FormatType.Command), // @brief after someCode
        new FormattedFragment(557, 4, FormatType.Command), // @ref
        new FormattedFragment(562, 7, FormatType.Parameter), // SomeRef of @ref
        new FormattedFragment(574, 2, FormatType.Command), // \p
        new FormattedFragment(577, 5, FormatType.Parameter), // Param of \p

        new FormattedFragment(617, 6, FormatType.Command), // @brief after /*!
        new FormattedFragment(645, 6, FormatType.Command), // @brief after /**
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
        new FormattedFragment(6, 6, FormatType.Command), // @brief
        new FormattedFragment(46, 10, FormatType.Command), // @param[in]
        new FormattedFragment(57, 7, FormatType.Parameter), // inParam of @param[in]
        new FormattedFragment(99, 8, FormatType.Warning), // @warning
        new FormattedFragment(151, 5, FormatType.Note), // \note
        new FormattedFragment(192, 7, FormatType.Command), // \tparam
        new FormattedFragment(200, 13, FormatType.Parameter), // templateParam of \tparam
        new FormattedFragment(266, 11, FormatType.Command), // \param[out]
        new FormattedFragment(278, 8, FormatType.Parameter), // outParam of \param[out]

        new FormattedFragment(360, 4, FormatType.Command), // @ref
        new FormattedFragment(365, 7, FormatType.Parameter), // SomeRef of @ref
        new FormattedFragment(377, 2, FormatType.Command), // \p
        new FormattedFragment(380, 5, FormatType.Parameter), // Param of \p

        new FormattedFragment(415, 6, FormatType.Command), // @brief
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
        new Utils.FormattedFragmentText(@"\addtogroup", FormatType.Command),
        new Utils.FormattedFragmentText("groupNameWithTitle", FormatType.Parameter),
        new Utils.FormattedFragmentText("Some group title", FormatType.Title),

        new Utils.FormattedFragmentText(@"\addtogroup", FormatType.Command),
        new Utils.FormattedFragmentText("groupNameWithoutTitle", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\callgraph", FormatType.Command),
        new Utils.FormattedFragmentText(@"\hidecallgraph", FormatType.Command),
        new Utils.FormattedFragmentText(@"@callergraph", FormatType.Command),
        new Utils.FormattedFragmentText(@"\hidecallergraph", FormatType.Command),
        new Utils.FormattedFragmentText(@"\showrefby", FormatType.Command),
        new Utils.FormattedFragmentText(@"\hiderefby", FormatType.Command),
        new Utils.FormattedFragmentText(@"\showrefs", FormatType.Command),
        new Utils.FormattedFragmentText(@"\hiderefs", FormatType.Command),

        new Utils.FormattedFragmentText(@"\category", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("inc/class.h", FormatType.Title),

        new Utils.FormattedFragmentText(@"\class", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatType.Title),
        new Utils.FormattedFragmentText(@"\class", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test2", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test3::MemClass", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test4", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test5", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\class", FormatType.Command),
        new Utils.FormattedFragmentText(@"\class", FormatType.Command),
        new Utils.FormattedFragmentText(@"\class", FormatType.Command),

        new Utils.FormattedFragmentText(@"\concept", FormatType.Command),
        new Utils.FormattedFragmentText(@"concept_name", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\def", FormatType.Command),
        new Utils.FormattedFragmentText(@"MAX(x,y)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\def", FormatType.Command),
        new Utils.FormattedFragmentText(@"\def", FormatType.Command),

        new Utils.FormattedFragmentText(@"\defgroup", FormatType.Command),
        new Utils.FormattedFragmentText(@"IntVariables", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"Global integer variables", FormatType.Title),

        new Utils.FormattedFragmentText(@"\dir", FormatType.Command),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\enum", FormatType.Command),
        new Utils.FormattedFragmentText(@"Enum_Test::TEnum", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\example", FormatType.Command),
        new Utils.FormattedFragmentText(@"example_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\example{lineno}", FormatType.Command),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\endinternal", FormatType.Command),

        new Utils.FormattedFragmentText(@"\extends", FormatType.Command),
        new Utils.FormattedFragmentText(@"Object", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\file", FormatType.Command),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\fileinfo{file}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{extension}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{filename}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{directory}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{full}", FormatType.Command),

        new Utils.FormattedFragmentText(@"\lineinfo", FormatType.Command),

        new Utils.FormattedFragmentText(@"\fn", FormatType.Command),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"some name\"", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText("\"test.h\"", FormatType.Parameter),
        new Utils.FormattedFragmentText("some name", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"\"", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText("\"test.h\"", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText("\"\"", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"<test.h>", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"test.h", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"<>", FormatType.Title),
        new Utils.FormattedFragmentText(@"\headerfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"<>", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\hideinitializer", FormatType.Command),

        new Utils.FormattedFragmentText(@"\idlexcept", FormatType.Command),
        new Utils.FormattedFragmentText(@"exception", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\implements", FormatType.Command),
        new Utils.FormattedFragmentText(@"ISomeInterface_", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\ingroup", FormatType.Command),
        new Utils.FormattedFragmentText(@"Group1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ingroup", FormatType.Command),
        new Utils.FormattedFragmentText(@"Group1 Group2 Group3", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\interface", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("inc/class.h", FormatType.Title),

        new Utils.FormattedFragmentText(@"\internal", FormatType.Command),

        new Utils.FormattedFragmentText(@"\mainpage", FormatType.Command),
        new Utils.FormattedFragmentText(@"My Personal Index Page", FormatType.Title),

        new Utils.FormattedFragmentText(@"\memberof", FormatType.Command),
        new Utils.FormattedFragmentText(@"_some_name", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\name", FormatType.Command),
        new Utils.FormattedFragmentText(@"group_", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\namespace", FormatType.Command),
        new Utils.FormattedFragmentText(@"nested::space", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\nosubgrouping", FormatType.Command),

        new Utils.FormattedFragmentText(@"\overload", FormatType.Command),
        new Utils.FormattedFragmentText(@"void Overload_Test::drawRect(const Rect &r)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\package", FormatType.Command),
        new Utils.FormattedFragmentText(@"PackageName", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\page", FormatType.Command),
        new Utils.FormattedFragmentText("page1", FormatType.Parameter),
        new Utils.FormattedFragmentText("A documentation page", FormatType.Title),

        new Utils.FormattedFragmentText(@"\private", FormatType.Command),
        new Utils.FormattedFragmentText(@"\privatesection", FormatType.Command),

        new Utils.FormattedFragmentText(@"\property", FormatType.Command),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\protected", FormatType.Command),
        new Utils.FormattedFragmentText(@"\protectedsection", FormatType.Command),

        new Utils.FormattedFragmentText(@"\protocol", FormatType.Command),
        new Utils.FormattedFragmentText(@"ProtocolName", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"Header.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("inc/Header.h", FormatType.Title),

        new Utils.FormattedFragmentText(@"\public", FormatType.Command),
        new Utils.FormattedFragmentText(@"\publicsection", FormatType.Command),

        new Utils.FormattedFragmentText(@"\pure", FormatType.Command),

        new Utils.FormattedFragmentText(@"\relates", FormatType.Command),
        new Utils.FormattedFragmentText(@"String", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\related", FormatType.Command),
        new Utils.FormattedFragmentText(@"String", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\relatesalso", FormatType.Command),
        new Utils.FormattedFragmentText(@"String", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\relatedalso", FormatType.Command),
        new Utils.FormattedFragmentText(@"String", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\showinitializer", FormatType.Command),
        new Utils.FormattedFragmentText(@"\static", FormatType.Command),

        new Utils.FormattedFragmentText(@"\typedef", FormatType.Command),
        new Utils.FormattedFragmentText(@"unsigned long ulong", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"@struct", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatType.Title),

        new Utils.FormattedFragmentText(@"\union", FormatType.Command),
        new Utils.FormattedFragmentText(@"Test1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"class.h", FormatType.Parameter),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", FormatType.Title),

        new Utils.FormattedFragmentText(@"\var", FormatType.Command),
        new Utils.FormattedFragmentText(@"unsigned long variable", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\weakgroup", FormatType.Command),
        new Utils.FormattedFragmentText("groupNameWithTitle", FormatType.Parameter),
        new Utils.FormattedFragmentText("Some group title", FormatType.Title),


        // --- Section indicators ---
        new Utils.FormattedFragmentText(@"\attention", FormatType.Note),
        new Utils.FormattedFragmentText(@"\author", FormatType.Command),
        new Utils.FormattedFragmentText(@"\authors", FormatType.Command),
        new Utils.FormattedFragmentText(@"\brief", FormatType.Command),
        new Utils.FormattedFragmentText(@"\bug", FormatType.Note),

        new Utils.FormattedFragmentText(@"\cond", FormatType.Command),
        new Utils.FormattedFragmentText(@"\cond", FormatType.Command),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\copyright", FormatType.Command),
        new Utils.FormattedFragmentText(@"\date", FormatType.Command),

        new Utils.FormattedFragmentText(@"\showdate", FormatType.Command),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"2015-3-14 03:04:15", FormatType.Title),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.Command),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""2015-3-14 03:04:15""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.Command),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.Command),
        new Utils.FormattedFragmentText(@"""""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"Format even empty """"", FormatType.Title),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.Command),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.Command),
        new Utils.FormattedFragmentText(@"\showdate", FormatType.Command),

        new Utils.FormattedFragmentText(@"\deprecated", FormatType.Note),
        new Utils.FormattedFragmentText(@"\details", FormatType.Command),
        new Utils.FormattedFragmentText(@"\noop", FormatType.Command),
        new Utils.FormattedFragmentText(@"\raisewarning", FormatType.Warning),
        new Utils.FormattedFragmentText(@"\else", FormatType.Command),

        new Utils.FormattedFragmentText(@"\elseif", FormatType.Command),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\endcond", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endif", FormatType.Command),

        new Utils.FormattedFragmentText(@"\exception", FormatType.Command),
        new Utils.FormattedFragmentText(@"std::out_of_range", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\if", FormatType.Command),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\if", FormatType.Command),
        new Utils.FormattedFragmentText(@"Cond1", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\ifnot", FormatType.Command),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\invariant", FormatType.Command),
        new Utils.FormattedFragmentText(@"\note", FormatType.Note),

        new Utils.FormattedFragmentText(@"\par", FormatType.Command),
        new Utils.FormattedFragmentText(@"User defined paragraph:", FormatType.Title),
        new Utils.FormattedFragmentText(@"\par", FormatType.Command),
        new Utils.FormattedFragmentText(@"\par", FormatType.Command),
        new Utils.FormattedFragmentText(@":  some title", FormatType.Title),

        new Utils.FormattedFragmentText(@"\param[out]", FormatType.Command),
        new Utils.FormattedFragmentText(@"dest", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param[in]", FormatType.Command),
        new Utils.FormattedFragmentText(@"src", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param[in]", FormatType.Command),
        new Utils.FormattedFragmentText(@"n", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param[in,out]", FormatType.Command),
        new Utils.FormattedFragmentText(@"p", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatType.Command),
        new Utils.FormattedFragmentText(@"p", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatType.Command),
        new Utils.FormattedFragmentText(@"x,y,z", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\param", FormatType.Command),
        new Utils.FormattedFragmentText(@"\param", FormatType.Command),
        new Utils.FormattedFragmentText(@"\param", FormatType.Command),

        new Utils.FormattedFragmentText(@"\parblock", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endparblock", FormatType.Command),

        new Utils.FormattedFragmentText(@"\tparam", FormatType.Command),
        new Utils.FormattedFragmentText(@"some_param", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\post", FormatType.Command),
        new Utils.FormattedFragmentText(@"\pre", FormatType.Command),
        new Utils.FormattedFragmentText(@"\remark", FormatType.Command),
        new Utils.FormattedFragmentText(@"\remarks", FormatType.Command),
        new Utils.FormattedFragmentText(@"\result", FormatType.Command),
        new Utils.FormattedFragmentText(@"\return", FormatType.Command),
        new Utils.FormattedFragmentText(@"\returns", FormatType.Command),

        new Utils.FormattedFragmentText(@"\retval", FormatType.Command),
        new Utils.FormattedFragmentText(@"some_value", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\sa", FormatType.Command),
        new Utils.FormattedFragmentText(@"\see", FormatType.Command),
        new Utils.FormattedFragmentText(@"\short", FormatType.Command),
        new Utils.FormattedFragmentText(@"\since", FormatType.Command),
        new Utils.FormattedFragmentText(@"\test", FormatType.Command),

        new Utils.FormattedFragmentText(@"\throw", FormatType.Command),
        new Utils.FormattedFragmentText(@"std::out_of_range", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\throws", FormatType.Command),
        new Utils.FormattedFragmentText(@"someException", FormatType.Parameter),
        new Utils.FormattedFragmentText("@throws", FormatType.Command),
        new Utils.FormattedFragmentText("std::runtime_error", FormatType.Parameter),
        new Utils.FormattedFragmentText("@ref", FormatType.Command),
        new Utils.FormattedFragmentText("someFunc()", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\todo", FormatType.Note),
        new Utils.FormattedFragmentText(@"\version", FormatType.Command),
        new Utils.FormattedFragmentText(@"\warning", FormatType.Warning),


        // --- Commands to create links ---

        new Utils.FormattedFragmentText(@"\addindex", FormatType.Command),

        new Utils.FormattedFragmentText(@"\anchor", FormatType.Command),
        new Utils.FormattedFragmentText(@"some_word", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\cite", FormatType.Command),
        new Utils.FormattedFragmentText(@"some_label", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\link", FormatType.Command),
        new Utils.FormattedFragmentText(@"link_obj", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\endlink", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endlink", FormatType.Command),

        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"subsection1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"subsection2", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"subsection3", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"link_text", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""some text""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"someFunc()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""some text 2""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"link_text3", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""some""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"link_text5", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class::Func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class::cls::func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class.Func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class.Func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class::Func(double,int)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"func(double, int)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class::Func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class::Func(int, double, cls::f)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"func()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class1", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class2", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"Class3", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"match", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),
        new Utils.FormattedFragmentText(@"\ref", FormatType.Command),

        new Utils.FormattedFragmentText(@"\refitem", FormatType.Command),
        new Utils.FormattedFragmentText(@"some_name", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\secreflist", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endsecreflist", FormatType.Command),

        new Utils.FormattedFragmentText(@"\subpage", FormatType.Command),
        new Utils.FormattedFragmentText(@"intro", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\subpage", FormatType.Command),
        new Utils.FormattedFragmentText(@"advanced", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""Advanced usage""", FormatType.Title),

        new Utils.FormattedFragmentText(@"\tableofcontents", FormatType.Command),

        new Utils.FormattedFragmentText(@"\section", FormatType.Command),
        new Utils.FormattedFragmentText(@"sec", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatType.Title),
        new Utils.FormattedFragmentText(@"\section", FormatType.Command),
        new Utils.FormattedFragmentText(@"\section", FormatType.Command),
        new Utils.FormattedFragmentText(@"\subsection", FormatType.Command),
        new Utils.FormattedFragmentText(@"sec_2", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\subsubsection", FormatType.Command),
        new Utils.FormattedFragmentText(@"sec", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatType.Title),
        new Utils.FormattedFragmentText(@"\paragraph", FormatType.Command),
        new Utils.FormattedFragmentText(@"sec", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"An example section", FormatType.Title),


        // --- Commands for displaying examples ---

        new Utils.FormattedFragmentText(@"\dontinclude", FormatType.Command),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dontinclude{lineno}", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\include", FormatType.Command),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\include{lineno}", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\include{doc}", FormatType.Command),
        new Utils.FormattedFragmentText(@"""some dir\include_test.cpp""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\includelineno", FormatType.Command),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\includedoc", FormatType.Command),
        new Utils.FormattedFragmentText(@"include_test.cpp", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\line", FormatType.Command),
        new Utils.FormattedFragmentText(@"example();", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\skip", FormatType.Command),
        new Utils.FormattedFragmentText(@"main", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\skipline", FormatType.Command),
        new Utils.FormattedFragmentText(@"Include_Test t;", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\until", FormatType.Command),
        new Utils.FormattedFragmentText(@"{", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\snippet", FormatType.Command),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"Adding a resource", FormatType.Title),
        new Utils.FormattedFragmentText(@"\snippet{lineno}", FormatType.Command),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatType.Title),
        new Utils.FormattedFragmentText(@"\snippet{doc}", FormatType.Command),
        new Utils.FormattedFragmentText(@"example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatType.Title),
        new Utils.FormattedFragmentText(@"\snippetlineno", FormatType.Command),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatType.Title),
        new Utils.FormattedFragmentText(@"\snippetdoc", FormatType.Command),
        new Utils.FormattedFragmentText(@"example.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"resource", FormatType.Title),

        new Utils.FormattedFragmentText(@"\verbinclude", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\htmlinclude", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\html.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\htmlinclude[block]", FormatType.Command),
        new Utils.FormattedFragmentText(@"html.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\latexinclude", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\tex.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\rtfinclude", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\rtf.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\maninclude", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\man.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\docbookinclude", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\doc.cpp", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\xmlinclude", FormatType.Command),
        new Utils.FormattedFragmentText(@"some dir\xml.cpp", FormatType.Parameter),


        // --- Commands for visual enhancements ---

        new Utils.FormattedFragmentText(@"\a", FormatType.Command),
        new Utils.FormattedFragmentText(@"x", FormatType.EmphasisMinor),
        new Utils.FormattedFragmentText(@"@a", FormatType.Command),
        new Utils.FormattedFragmentText(@"y::p", FormatType.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\b", FormatType.Command),
        new Utils.FormattedFragmentText(@"x", FormatType.EmphasisMajor),
        new Utils.FormattedFragmentText(@"@b", FormatType.Command),
        new Utils.FormattedFragmentText(@"y::p", FormatType.EmphasisMajor),

        new Utils.FormattedFragmentText(@"\c", FormatType.Command),
        new Utils.FormattedFragmentText(@"x", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"@c", FormatType.Command),
        new Utils.FormattedFragmentText(@"y::p", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\p", FormatType.Command),
        new Utils.FormattedFragmentText(@"x", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"@p", FormatType.Command),
        new Utils.FormattedFragmentText(@"y::p", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\p", FormatType.Command),
        new Utils.FormattedFragmentText(@"::thing", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\p", FormatType.Command),
        new Utils.FormattedFragmentText(@"\p", FormatType.Command),
        new Utils.FormattedFragmentText(@"\p", FormatType.Command),
        new Utils.FormattedFragmentText(@"thing", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\arg", FormatType.Command),
        new Utils.FormattedFragmentText(@"\li", FormatType.Command),

        new Utils.FormattedFragmentText(@"\code{.py}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endcode", FormatType.Command),
        new Utils.FormattedFragmentText(@"\code{.cpp}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endcode", FormatType.Command),
        new Utils.FormattedFragmentText(@"\code{.unparsed}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endcode", FormatType.Command),
        new Utils.FormattedFragmentText(@"\code", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endcode", FormatType.Command),

        new Utils.FormattedFragmentText(@"@copydoc", FormatType.Command),
        new Utils.FormattedFragmentText(@"MyClass::myfunction(type1,type2)", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"@copydoc", FormatType.Command),
        new Utils.FormattedFragmentText(@"MyClass::myfunction()", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\brief", FormatType.Command),
        new Utils.FormattedFragmentText(@"\copybrief", FormatType.Command),
        new Utils.FormattedFragmentText(@"foo()", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\details", FormatType.Command),
        new Utils.FormattedFragmentText(@"\copydetails", FormatType.Command),
        new Utils.FormattedFragmentText(@"foo()", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\docbookonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\enddocbookonly", FormatType.Command),

        new Utils.FormattedFragmentText(@"\dot", FormatType.Command),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatType.Command),
        new Utils.FormattedFragmentText(@"width=2\textwidth", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatType.Command),
        new Utils.FormattedFragmentText(@"""foo""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatType.Command),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"height=\textwidth", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dot", FormatType.Command),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\dot", FormatType.Command),
        new Utils.FormattedFragmentText(@"\dot", FormatType.Command),

        new Utils.FormattedFragmentText(@"\enddot", FormatType.Command),

        new Utils.FormattedFragmentText(@"\emoji", FormatType.Command),
        new Utils.FormattedFragmentText(@":smile:", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\emoji", FormatType.Command),
        new Utils.FormattedFragmentText(@"left_luggage", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"@msc", FormatType.Command),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\msc", FormatType.Command),

        new Utils.FormattedFragmentText(@"\endmsc", FormatType.Command),

        new Utils.FormattedFragmentText(@"\startuml", FormatType.Command),
        new Utils.FormattedFragmentText(@"@startuml{myimage.png}", FormatType.Command),
        new Utils.FormattedFragmentText(@"""Image Caption""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"@startuml{json, myimage.png}", FormatType.Command),
        new Utils.FormattedFragmentText(@"""Image Caption""", FormatType.Title),
        new Utils.FormattedFragmentText(@"@startuml{json}", FormatType.Command),

        new Utils.FormattedFragmentText(@"@enduml", FormatType.Command),

        new Utils.FormattedFragmentText(@"\dotfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"filename", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"""file name""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""foo  test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"filename", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"""file name""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"""file name""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"filename", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""foo test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"\dotfile", FormatType.Command),

        new Utils.FormattedFragmentText(@"\mscfile", FormatType.Command),
        new Utils.FormattedFragmentText(@"file_name.msc", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""test""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\diafile", FormatType.Command),
        new Utils.FormattedFragmentText(@"""path\with space\file_name.dia""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\e", FormatType.Command),
        new Utils.FormattedFragmentText(@"really", FormatType.EmphasisMinor),
        new Utils.FormattedFragmentText(@"\em", FormatType.Command),
        new Utils.FormattedFragmentText(@"x", FormatType.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\htmlonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\htmlonly[block]", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endhtmlonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\latexonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endlatexonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\manonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endmanonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\rtfonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endrtfonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\verbatim", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endverbatim", FormatType.Command),
        new Utils.FormattedFragmentText(@"\xmlonly", FormatType.Command),
        new Utils.FormattedFragmentText(@"\endxmlonly", FormatType.Command),

        new Utils.FormattedFragmentText(@"\f$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f(", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f)", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f[", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f]", FormatType.Command),

        new Utils.FormattedFragmentText(@"\f{eqnarray*}{", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f{eqnarray*}", FormatType.Command),
        new Utils.FormattedFragmentText(@"\f}", FormatType.Command),

        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"html", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"html", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"application.jpg", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"latex", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"application.eps", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""My application""", FormatType.Title),
        new Utils.FormattedFragmentText(@"width=10cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"docbook", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""file name.eps""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"width=200cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image{inline,anchor:id}", FormatType.Command),
        new Utils.FormattedFragmentText(@"rtf", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"""path with space/name.rtf""", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image{inline}", FormatType.Command),
        new Utils.FormattedFragmentText(@"xml", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"file.xml", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"height=1cm", FormatType.Parameter),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
        new Utils.FormattedFragmentText(@"\image", FormatType.Command),

        new Utils.FormattedFragmentText(@"\n", FormatType.Command),
        new Utils.FormattedFragmentText(@"\n", FormatType.Command),
        new Utils.FormattedFragmentText(@"\n", FormatType.Command),
        new Utils.FormattedFragmentText(@"\n", FormatType.Command),

        new Utils.FormattedFragmentText(@"\@", FormatType.Command),

        new Utils.FormattedFragmentText(@"\~", FormatType.Command),
        new Utils.FormattedFragmentText(@"\~english", FormatType.Command),
        new Utils.FormattedFragmentText(@"\~", FormatType.Command),
        new Utils.FormattedFragmentText(@"\~english", FormatType.Command),
        new Utils.FormattedFragmentText(@"\~dutch", FormatType.Command),
        new Utils.FormattedFragmentText(@"\~german", FormatType.Command),
        new Utils.FormattedFragmentText(@"\~", FormatType.Command),

        new Utils.FormattedFragmentText(@"\&", FormatType.Command),
        new Utils.FormattedFragmentText(@"\$", FormatType.Command),
        new Utils.FormattedFragmentText(@"\#", FormatType.Command),
        new Utils.FormattedFragmentText(@"\<", FormatType.Command),
        new Utils.FormattedFragmentText(@"\>", FormatType.Command),
        new Utils.FormattedFragmentText(@"\%", FormatType.Command),
        new Utils.FormattedFragmentText(@"\.", FormatType.Command),
        new Utils.FormattedFragmentText(@"\=", FormatType.Command),
        new Utils.FormattedFragmentText(@"\::", FormatType.Command),
        new Utils.FormattedFragmentText(@"\|", FormatType.Command),
        new Utils.FormattedFragmentText(@"\--", FormatType.Command),
        new Utils.FormattedFragmentText(@"\---", FormatType.Command),
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
        new Utils.FormattedFragmentText("@param", FormatType.Command),
        new Utils.FormattedFragmentText("t\U0001F600t", FormatType.Parameter),

        new Utils.FormattedFragmentText(@"\image", FormatType.Command),
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
