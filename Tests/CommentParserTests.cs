using Microsoft.VisualStudio.TestTools.UnitTesting;
using VSDoxyHighlighter;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using static System.Collections.Specialized.BitVector32;
using System.Text.RegularExpressions;

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


    public static List<FormattedFragmentText> ConvertToTextFragments(string text, ICollection<FormattedFragment> fragments)
    {
      var result = new List<FormattedFragmentText>();
      foreach (FormattedFragment fragment in fragments) {
        Assert.IsTrue(fragment.Length > 0);
        Assert.IsTrue(fragment.EndIndex < text.Length);
        string str = text.Substring(fragment.StartIndex, fragment.Length);
        result.Add(new FormattedFragmentText(str, fragment.Classification));
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


    private class IGeneralOptionsFake : IGeneralOptions
    {
      public bool EnableHighlighting { get; } = true;
      public bool EnableAutocomplete { get; } = true;

      public List<DoxygenCommandInConfig> DoxygenCommandsConfig { get; }

      public bool IsEnabledInCommentType(CommentType type) { return true; }

#pragma warning disable 67
      public event EventHandler SettingsChanged;
#pragma warning restore 67

      public IGeneralOptionsFake()
      {
        DoxygenCommandsConfig = DoxygenCommands.DefaultDoxygenCommandsInConfig;
      }
    }

    public static CommentParser CreateDefaultCommentParser() 
    {
      return new CommentParser(new DoxygenCommands(new IGeneralOptionsFake()));
    }
  }


  [TestClass()]
  public class CommentParserTests
  {
    [TestMethod()]
    public void EmptyStringShouldCauseNoFormatting()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse("");
      Assert.IsNotNull(actualFragments);
      Assert.AreEqual(0, actualFragments.Count);
    }


    [TestMethod()]
    public void BasicCStyleCommentsShouldBeFormatted()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("BasicCStyleFormatting.cpp"));

      var expectedFragments = new List<FormattedFragment>() {
        new FormattedFragment(8, 6, ClassificationEnum.Command1), // @brief
        new FormattedFragment(47, 8, ClassificationEnum.Command1), // @details
        new FormattedFragment(88, 10, ClassificationEnum.Command1), // @param[in]
        new FormattedFragment(99, 7, ClassificationEnum.Parameter1), // inParam of the @param[in]
        new FormattedFragment(139, 8, ClassificationEnum.Warning), // @warning
        new FormattedFragment(180, 5, ClassificationEnum.Note), // @note

        new FormattedFragment(221, 7, ClassificationEnum.Command1), // \tparam
        new FormattedFragment(229, 13, ClassificationEnum.Parameter1), // templateParam of \tparam
        new FormattedFragment(292, 11, ClassificationEnum.Command1), // \param[out]
        new FormattedFragment(304, 8, ClassificationEnum.Parameter1), // outParam of the \param[out]
        new FormattedFragment(357, 8, ClassificationEnum.Command1), // \returns
        new FormattedFragment(396, 7, ClassificationEnum.Command1), // \return
        new FormattedFragment(433, 4, ClassificationEnum.Command1), // \see

        new FormattedFragment(490, 6, ClassificationEnum.Command1), // @brief after someCode
        new FormattedFragment(557, 4, ClassificationEnum.Command1), // @ref
        new FormattedFragment(562, 7, ClassificationEnum.Parameter2), // SomeRef of @ref
        new FormattedFragment(574, 2, ClassificationEnum.Command1), // \p
        new FormattedFragment(577, 5, ClassificationEnum.Parameter2), // Param of \p

        new FormattedFragment(617, 6, ClassificationEnum.Command1), // @brief after /*!
        new FormattedFragment(645, 6, ClassificationEnum.Command1), // @brief after /**
      };

      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void BasicCppStyleCommentsShouldBeFormatted()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("BasicCppStyleFormatting.cpp"));

      var expectedFragments = new List<FormattedFragment>() {
        new FormattedFragment(6, 6, ClassificationEnum.Command1), // @brief
        new FormattedFragment(46, 10, ClassificationEnum.Command1), // @param[in]
        new FormattedFragment(57, 7, ClassificationEnum.Parameter1), // inParam of @param[in]
        new FormattedFragment(99, 8, ClassificationEnum.Warning), // @warning
        new FormattedFragment(151, 5, ClassificationEnum.Note), // \note
        new FormattedFragment(192, 7, ClassificationEnum.Command1), // \tparam
        new FormattedFragment(200, 13, ClassificationEnum.Parameter1), // templateParam of \tparam
        new FormattedFragment(266, 11, ClassificationEnum.Command1), // \param[out]
        new FormattedFragment(278, 8, ClassificationEnum.Parameter1), // outParam of \param[out]

        new FormattedFragment(360, 4, ClassificationEnum.Command1), // @ref
        new FormattedFragment(365, 7, ClassificationEnum.Parameter2), // SomeRef of @ref
        new FormattedFragment(377, 2, ClassificationEnum.Command1), // \p
        new FormattedFragment(380, 5, ClassificationEnum.Parameter2), // Param of \p

        new FormattedFragment(415, 6, ClassificationEnum.Command1), // @brief
      };

      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    List<Utils.FormattedFragmentText> GetExpectedTextFragmentsForVariousKeywordsTests()
    {
      var expectedTextFragments = new List<Utils.FormattedFragmentText>() {
        // --- Structural indicators --- 
        new Utils.FormattedFragmentText(@"\addtogroup", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("groupNameWithTitle", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("Some group title", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\addtogroup", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("groupNameWithoutTitle", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\callgraph", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\hidecallgraph", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"@callergraph", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\hidecallergraph", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\showrefby", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\hiderefby", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\showrefs", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\hiderefs", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"quali", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""SOMEQUALI text""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""Another""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""yet""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""more text""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("\"\"", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\category", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("inc/class.h", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test2", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test3::MemClass", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test4", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test5", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\concept", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"concept_name", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\def", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"MAX(x,y)", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\def", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\def", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\defgroup", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"IntVariables", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Global integer variables", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\dir", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\enum", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Enum_Test::TEnum", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\example", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"example_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\example{lineno}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\endinternal", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\extends", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Object", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\file", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\fileinfo", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\fileinfo{file}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\fileinfo{extension}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\fileinfo{filename}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\fileinfo{directory}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\fileinfo{full}", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\lineinfo", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\fn", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("\"some name\"", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("\"test.h\"", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("some name", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("\"\"", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("\"test.h\"", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("\"\"", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"<test.h>", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"<>", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"<>", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\hideinitializer", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\idlexcept", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText(@"exception", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\implements", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"ISomeInterface_", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\ingroup", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Group1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\ingroup", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Group1 Group2 Group3", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\interface", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("inc/class.h", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\internal", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\mainpage", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"My Personal Index Page", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\memberof", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"_some_name", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\name", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\name", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some group title", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\namespace", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"nested::space", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\nosubgrouping", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\overload", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"void Overload_Test::drawRect(const Rect &r)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\package", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"PackageName", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\page", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("page1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("A documentation page", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\private", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\privatesection", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\property", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\protected", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\protectedsection", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\protocol", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"ProtocolName", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Header.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("inc/Header.h", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\public", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\publicsection", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\pure", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\relates", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"String", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\related", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"String", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\relatesalso", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"String", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\relatedalso", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"String", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\showinitializer", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\static", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\typedef", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"unsigned long ulong", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"@struct", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\union", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\var", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"unsigned long variable", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\weakgroup", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("groupNameWithTitle", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("Some group title", ClassificationEnum.Title),


        // --- Section indicators ---
        new Utils.FormattedFragmentText(@"\attention", ClassificationEnum.Note),
        new Utils.FormattedFragmentText(@"\author", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\authors", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\brief", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\bug", ClassificationEnum.Note),

        new Utils.FormattedFragmentText(@"\cond", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\cond", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\copyright", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\date", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"2015-3-14 03:04:15", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""2015-3-14 03:04:15""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Format even empty """"", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\deprecated", ClassificationEnum.Note),
        new Utils.FormattedFragmentText(@"\details", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\noop", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\raisewarning", ClassificationEnum.Warning),
        new Utils.FormattedFragmentText(@"\else", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\elseif", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\endcond", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endif", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\exception", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText(@"std::out_of_range", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\if", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\if", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Cond1", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\ifnot", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\invariant", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\note", ClassificationEnum.Note),

        new Utils.FormattedFragmentText(@"\par", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"User defined paragraph:", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\par", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\par", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@":  some title", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\param[out]", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"dest", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param[in]", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"src", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param[in]", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"n", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param[in,out]", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"p", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"p", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"x,y,z", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\parblock", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endparblock", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\tparam", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some_param", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\post", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\pre", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\remark", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\remarks", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\result", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\return", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\returns", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\retval", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some_value", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\sa", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\see", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\short", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\since", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\test", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\throw", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText(@"std::out_of_range", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\throws", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText(@"someException", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("@throws", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText("std::runtime_error", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("@ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("someFunc()", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\todo", ClassificationEnum.Note),
        new Utils.FormattedFragmentText(@"\version", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\warning", ClassificationEnum.Warning),


        // --- Commands to create links ---

        new Utils.FormattedFragmentText(@"\addindex", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\anchor", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some_word", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\cite", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some_label", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\link", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"link_obj", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\endlink", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endlink", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"subsection1", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"subsection2", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"subsection3", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"link_text", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""some text""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"someFunc()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""some text 2""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"link_text3", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""some""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"link_text5", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class::Func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class::cls::func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class.Func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class.Func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class::Func(double,int)", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"func(double, int)", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class::Func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class::Func(int, double, cls::f)", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class1", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class2", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Class3", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"match", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\refitem", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some_name", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\secreflist", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endsecreflist", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\subpage", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"intro", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\subpage", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"advanced", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""Advanced usage""", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\tableofcontents", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\section", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"sec", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"An example section", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\section", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\section", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\subsection", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"sec_2", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\subsubsection", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"sec", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"An example section", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\paragraph", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"sec", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"An example section", ClassificationEnum.Title),


        // --- Commands for displaying examples ---

        new Utils.FormattedFragmentText(@"\dontinclude", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dontinclude{lineno}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include{lineno}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include{doc}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""some dir\include_test.cpp""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\includelineno", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\includedoc", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\line", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"example();", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\skip", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"main", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\skipline", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"Include_Test t;", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\until", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"{", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Adding a resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet{lineno}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet{doc}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippetlineno", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippetdoc", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\verbinclude", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\htmlinclude", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\html.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\htmlinclude[block]", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"html.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\latexinclude", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\tex.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\rtfinclude", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\rtf.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\maninclude", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\man.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\docbookinclude", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\doc.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\xmlinclude", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"some dir\xml.cpp", ClassificationEnum.Parameter1),


        // --- Commands for visual enhancements ---

        new Utils.FormattedFragmentText(@"\a", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.EmphasisMinor),
        new Utils.FormattedFragmentText(@"@a", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"y::p", ClassificationEnum.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\b", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.EmphasisMajor),
        new Utils.FormattedFragmentText(@"@b", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"y::p", ClassificationEnum.EmphasisMajor),

        new Utils.FormattedFragmentText(@"\c", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"@c", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"y::p", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"@p", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"y::p", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"::thing", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"thing", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\arg", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\li", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\code{.py}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\code{.c}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\code{.cpp}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\code{.c++}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\code{.unparsed}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\code", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"@copydoc", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"MyClass::myfunction(type1,type2)", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"@copydoc", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"MyClass::myfunction()", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\brief", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\copybrief", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"foo()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\details", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\copydetails", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"foo()", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\docbookonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\enddocbookonly", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"width=2\textwidth", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""foo""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"height=\textwidth", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\enddot", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\emoji", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@":smile:", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\emoji", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"left_luggage", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"@msc", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\msc", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\endmsc", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\startuml", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"@startuml{myimage.png}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""Image Caption""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"@startuml{json, myimage.png}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""Image Caption""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"@startuml{json}", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"@enduml", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"filename", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""file name""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""foo  test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"filename", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""file name""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""file name""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"filename", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\mscfile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"file_name.msc", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\diafile", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"""path\with space\file_name.dia""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\e", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"really", ClassificationEnum.EmphasisMinor),
        new Utils.FormattedFragmentText(@"\em", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\htmlonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\htmlonly[block]", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endhtmlonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\latexonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endlatexonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\manonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endmanonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\rtfonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endrtfonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\verbatim", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endverbatim", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\xmlonly", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\endxmlonly", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f(", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f)", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f[", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f]", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\f{eqnarray*}{", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f{eqnarray*}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\f}", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"html", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"html", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"application.jpg", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"latex", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"application.eps", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""My application""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=10cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"docbook", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""file name.eps""", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\image{inline,anchor:id}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"rtf", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""path with space/name.rtf""", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\image{inline}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"xml", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"file.xml", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\n", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\n", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\n", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\n", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\@", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\~", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\~english", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\~", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\~english", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\~dutch", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\~german", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\~", ClassificationEnum.Command1),

        new Utils.FormattedFragmentText(@"\&", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\$", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\#", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\<", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\>", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\%", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\.", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\=", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\::", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\|", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\--", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\---", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\{", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"\}", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"@{", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"@}", ClassificationEnum.Command1),


        // --- Additional stuff ---

        new Utils.FormattedFragmentText(@"\ingroup", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"foo  ", ClassificationEnum.Parameter1),

      };

      return expectedTextFragments;
    }


    [TestMethod()]
    public void VariousKeywordsShouldBeFormatted_CRLF()
    {
      var input = Utils.ReadTestInputFromFile("VariousKeywords.cpp");
      Assert.IsTrue(Regex.Matches(input, "\r\n").Count > 10); // Cross-check the input file

      var actualFragments = Utils.CreateDefaultCommentParser().Parse(input);

      var expectedTextFragments = GetExpectedTextFragmentsForVariousKeywordsTests();
      var actualTextFragments = Utils.ConvertToTextFragments(input, actualFragments);

      // Write fragments to file for easy checking of test failures.
      Utils.WriteFragmentsToFile("VariousKeywords_Expected.txt", expectedTextFragments);
      Utils.WriteFragmentsToFile("VariousKeywords_Actual.txt", actualTextFragments);

      CollectionAssert.AreEquivalent(expectedTextFragments, actualTextFragments);
    }


    [TestMethod()]
    public void VariousKeywordsShouldBeFormatted_LF()
    {
      var input = Utils.ReadTestInputFromFile("VariousKeywords.cpp");
      input = input.Replace("\r\n", "\n");
      Assert.IsTrue(Regex.Matches(input, "\r").Count == 0); // Cross-check

      var expectedTextFragments = GetExpectedTextFragmentsForVariousKeywordsTests();
      var actualTextFragments = Utils.ConvertToTextFragments(input, Utils.CreateDefaultCommentParser().Parse(input));

      CollectionAssert.AreEquivalent(expectedTextFragments, actualTextFragments);
    }


    [TestMethod()]
    public void CasesWhereNothingShouldBeFormatted()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("NothingToFormat.cpp"));

      Assert.AreEqual(0, actualFragments.Count);
    }


    [TestMethod()]
    public void SingleStarShouldFormatItalic()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_SingleStar.cpp"));

      var expectedFragments = GetExpectationsForItalic();
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void SingleUnderscoreShouldFormatItalic()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_SingleStar.cpp"));

      var expectedFragments = GetExpectationsForItalic();
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    private List<FormattedFragment> GetExpectationsForItalic()
    {
      return new List<FormattedFragment>() {
        new FormattedFragment(9, 8, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(33, 8, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(52, 13, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(76, 16, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(110, 13, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(140, 8, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(155, 8, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(178, 10, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(207, 15, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(230, 11, ClassificationEnum.EmphasisMinor),
        new FormattedFragment(248, 3, ClassificationEnum.EmphasisMinor),
      };
    }


    [TestMethod()]
    public void DoubleStarShouldFormatBold()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_DoubleStar.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(ClassificationEnum.EmphasisMajor);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void DoubleUnderscoreShouldFormatBold()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_DoubleUnderscore.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(ClassificationEnum.EmphasisMajor);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void DoubleTildeShouldFormatStrikethrough()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_DoubleTilde.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(ClassificationEnum.Strikethrough);
      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    private List<FormattedFragment> GetExpectationsForBoldOrStrikethrough(ClassificationEnum expectedFormat)
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
        new FormattedFragment(250, 5, expectedFormat),
      };
    }


    [TestMethod()]
    public void InlineCodeShouldBeFormatted()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_InlineCode.cpp"));

      var expectedFragments = new List<FormattedFragment>() {
        new FormattedFragment(4, 13, ClassificationEnum.InlineCode),
        new FormattedFragment(30, 8, ClassificationEnum.InlineCode),
        new FormattedFragment(48, 13, ClassificationEnum.InlineCode),
        new FormattedFragment(72, 6, ClassificationEnum.InlineCode),
        new FormattedFragment(94, 2, ClassificationEnum.InlineCode),
        new FormattedFragment(114, 7, ClassificationEnum.InlineCode),
        new FormattedFragment(121, 7, ClassificationEnum.InlineCode),
        new FormattedFragment(134, 26, ClassificationEnum.InlineCode),
        new FormattedFragment(167, 45, ClassificationEnum.InlineCode),
      };

      CollectionAssert.AreEquivalent(expectedFragments, actualFragments);
    }


    [TestMethod()]
    public void UnicodeParametersShouldWork()
    {
      var input = Utils.ReadTestInputFromFile("UnicodeParametersUTF8.cpp");
      var actualFragments = Utils.CreateDefaultCommentParser().Parse(input);

      var expectedTextFragments = new List<Utils.FormattedFragmentText>() {
        new Utils.FormattedFragmentText("@param", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText("t\U0001F600t", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command1),
        new Utils.FormattedFragmentText(@"latex", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("\"file \U0001F600 name.eps\"", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("\"test\U0001F600\"", ClassificationEnum.Title),

        new Utils.FormattedFragmentText("**te\U0001F600st**", ClassificationEnum.EmphasisMajor),
      };

      var actualTextFragments = Utils.ConvertToTextFragments(input, actualFragments);
      CollectionAssert.AreEquivalent(expectedTextFragments, actualTextFragments);
    }
  }
}
