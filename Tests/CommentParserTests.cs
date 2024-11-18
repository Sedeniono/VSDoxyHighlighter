using Microsoft.VisualStudio.TestTools.UnitTesting;
using VSDoxyHighlighter;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using static System.Collections.Specialized.BitVector32;
using System.Text.RegularExpressions;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

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


  [TestClass()]
  public class CommentParserTests
  {
    [TestMethod()]
    public void EmptyStringShouldCauseNoFormatting()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse("");
      Assert.IsNotNull(actualFragments);
      Assert.AreEqual(0, actualFragments.Count());
    }


    [TestMethod()]
    public void BasicCStyleCommentsShouldBeFormatted()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragmentGroups = formatter.Parse(
        Utils.ReadTestInputFromFile("BasicCStyleFormatting.cpp"));

      var expectedFragments = new List<FormattedFragment>() {
        new FormattedFragment(8, 6, ClassificationEnum.Command), // @brief
        new FormattedFragment(47, 8, ClassificationEnum.Command), // @details
        new FormattedFragment(88, 6, ClassificationEnum.Command), // @param
        new FormattedFragment(94, 4, ClassificationEnum.ParameterClamped), // [in] of @param
        new FormattedFragment(99, 7, ClassificationEnum.Parameter1), // inParam of the @param[in]
        new FormattedFragment(139, 8, ClassificationEnum.Warning), // @warning
        new FormattedFragment(180, 5, ClassificationEnum.Note), // @note

        new FormattedFragment(221, 7, ClassificationEnum.Command), // \tparam
        new FormattedFragment(229, 13, ClassificationEnum.Parameter1), // templateParam of \tparam
        new FormattedFragment(292, 6, ClassificationEnum.Command), // \param
        new FormattedFragment(298, 5, ClassificationEnum.ParameterClamped), // [out] of \param
        new FormattedFragment(304, 8, ClassificationEnum.Parameter1), // outParam of the \param[out]
        new FormattedFragment(357, 8, ClassificationEnum.Command), // \returns
        new FormattedFragment(396, 7, ClassificationEnum.Command), // \return
        new FormattedFragment(433, 4, ClassificationEnum.Command), // \see

        new FormattedFragment(490, 6, ClassificationEnum.Command), // @brief after someCode
        new FormattedFragment(557, 4, ClassificationEnum.Command), // @ref
        new FormattedFragment(562, 7, ClassificationEnum.Parameter2), // SomeRef of @ref
        new FormattedFragment(574, 2, ClassificationEnum.Command), // \p
        new FormattedFragment(577, 5, ClassificationEnum.Parameter2), // Param of \p

        new FormattedFragment(617, 6, ClassificationEnum.Command), // @brief after /*!
        new FormattedFragment(645, 6, ClassificationEnum.Command), // @brief after /**
      };

      CollectionAssert.AreEqual(expectedFragments, Utils.ToFlatFragmentList(actualFragmentGroups));
    }


    [TestMethod()]
    public void BasicCppStyleCommentsShouldBeFormatted()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragmentGroups = formatter.Parse(
        Utils.ReadTestInputFromFile("BasicCppStyleFormatting.cpp"));

      var expectedFragments = new List<FormattedFragment>() {
        new FormattedFragment(6, 6, ClassificationEnum.Command), // @brief
        new FormattedFragment(46, 6, ClassificationEnum.Command), // @param
        new FormattedFragment(52, 4, ClassificationEnum.ParameterClamped), // [in] of @param
        new FormattedFragment(57, 7, ClassificationEnum.Parameter1), // inParam of @param[in]
        new FormattedFragment(99, 8, ClassificationEnum.Warning), // @warning
        new FormattedFragment(151, 5, ClassificationEnum.Note), // \note
        new FormattedFragment(192, 7, ClassificationEnum.Command), // \tparam
        new FormattedFragment(200, 13, ClassificationEnum.Parameter1), // templateParam of \tparam
        new FormattedFragment(266, 6, ClassificationEnum.Command), // \param
        new FormattedFragment(272, 5, ClassificationEnum.ParameterClamped), // [out] of \param
        new FormattedFragment(278, 8, ClassificationEnum.Parameter1), // outParam of \param[out]

        new FormattedFragment(360, 4, ClassificationEnum.Command), // @ref
        new FormattedFragment(365, 7, ClassificationEnum.Parameter2), // SomeRef of @ref
        new FormattedFragment(377, 2, ClassificationEnum.Command), // \p
        new FormattedFragment(380, 5, ClassificationEnum.Parameter2), // Param of \p

        new FormattedFragment(415, 6, ClassificationEnum.Command), // @brief
      };

      CollectionAssert.AreEqual(expectedFragments, Utils.ToFlatFragmentList(actualFragmentGroups));
    }


    List<Utils.FormattedFragmentText> GetExpectedTextFragmentsForVariousKeywordsTests()
    {
      var expectedTextFragments = new List<Utils.FormattedFragmentText>() {
        // --- Structural indicators --- 
        new Utils.FormattedFragmentText(@"\addtogroup", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("groupNameWithTitle", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("Some group title", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\addtogroup", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("groupNameWithoutTitle", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\callgraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\hidecallgraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@callergraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\hidecallergraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\showrefby", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\hiderefby", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\showrefs", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\hiderefs", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"@showinlinesource", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@hideinlinesource", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\includegraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@hideincludegraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@includedbygraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\hideincludedbygraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\directorygraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\hidedirectorygraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\collaborationgraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\hidecollaborationgraph", ClassificationEnum.Command),
        
        new Utils.FormattedFragmentText(@"\inheritancegraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\inheritancegraph{NO}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@inheritancegraph{YES}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\inheritancegraph{TEXT}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\inheritancegraph{GRAPH}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\inheritancegraph{BUILTIN}", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\hideinheritancegraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\groupgraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\hidegroupgraph", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"quali", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""SOMEQUALI text""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""Another""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""yet""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""more text""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("\"\"", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\qualifier", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\category", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("inc/class.h", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test2", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test3::MemClass", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test4", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test5", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\class", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\concept", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"concept_name", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\def", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"MAX(x,y)", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\def", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\def", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\defgroup", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"IntVariables", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Global integer variables", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\dir", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\enum", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Enum_Test::TEnum", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\example", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"example_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\example{lineno}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\endinternal", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\extends", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Object", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\file", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"path with spaces\example_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\fileinfo", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{file}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{extension}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{filename}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{directory}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\fileinfo{full}", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\lineinfo", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\fn", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("\"some name\"", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("\"test.h\"", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("some name", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("\"\"", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("\"test.h\"", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("\"\"", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"<test.h>", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"test.h", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"<>", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\headerfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"<>", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\hideinitializer", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\idlexcept", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText(@"exception", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\implements", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"ISomeInterface_", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\ingroup", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Group1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\ingroup", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Group1 Group2 Group3", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\interface", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("inc/class.h", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\internal", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\mainpage", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"My Personal Index Page", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\memberof", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"_some_name", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\module", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@module", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"my_module", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\name", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\name", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some group title", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\namespace", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"nested::space", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\nosubgrouping", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\overload", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"void Overload_Test::drawRect(const Rect &r)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\package", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"PackageName", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\page", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("page1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("A documentation page", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\private", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\privatesection", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\property", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"const char *Fn_Test::member(char c,int n)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\protected", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\protectedsection", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\protocol", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"ProtocolName", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Header.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("inc/Header.h", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\public", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\publicsection", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\pure", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\relates", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"String", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\related", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"String", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\relatesalso", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"String", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\relatedalso", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"String", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\showinitializer", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\static", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\typedef", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"unsigned long ulong", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"@struct", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\union", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Test1", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"class.h", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("\"inc dir/class.h\"", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\var", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"unsigned long variable", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\weakgroup", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("groupNameWithTitle", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("Some group title", ClassificationEnum.Title),


        // --- Section indicators ---
        new Utils.FormattedFragmentText(@"\attention", ClassificationEnum.Note),
        new Utils.FormattedFragmentText(@"\author", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\authors", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\brief", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\bug", ClassificationEnum.Note),

        new Utils.FormattedFragmentText(@"\cond", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\cond", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\copyright", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\date", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"2015-3-14 03:04:15", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""2015-3-14 03:04:15""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""%A %d-%m-%Y %H:%M:%S""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Format even empty """"", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\showdate", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\deprecated", ClassificationEnum.Note),
        new Utils.FormattedFragmentText(@"\details", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\noop", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\raisewarning", ClassificationEnum.Warning),
        new Utils.FormattedFragmentText(@"\else", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\elseif", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\endcond", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endif", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\exception", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText(@"std::out_of_range", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\if", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\if", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Cond1", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\ifnot", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"(!LABEL1 && LABEL2)", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\important", ClassificationEnum.Note),
        new Utils.FormattedFragmentText(@"\invariant", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\note", ClassificationEnum.Note),

        new Utils.FormattedFragmentText(@"\par", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"User defined paragraph:", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\par", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\par", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@":  some title", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[out]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"dest", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[in]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"src", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[in]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"n", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[in,out]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"p", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[ in  ]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"test", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[ out 	 ]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"test", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[in,	out ]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"test", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[out,in]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"test", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[out, in]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"test", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[ out, in	]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"test", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"p", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"x,y,z", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[ out , in	]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"[ out, in]", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"\param", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\parblock", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endparblock", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\tparam", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some_param", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\post", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\pre", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\remark", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\remarks", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\result", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\return", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\returns", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\retval", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some_value", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\sa", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\see", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\short", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\since", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\test", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\throw", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText(@"std::out_of_range", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\throws", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText(@"someException", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("@throws", ClassificationEnum.Exceptions),
        new Utils.FormattedFragmentText("std::runtime_error", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("@ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("someFunc()", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\todo", ClassificationEnum.Note),
        new Utils.FormattedFragmentText(@"\version", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\warning", ClassificationEnum.Warning),


        // --- Commands to create links ---

        new Utils.FormattedFragmentText(@"\addindex", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\anchor", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some_word", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\cite", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some_label", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\link", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"link_obj", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\endlink", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endlink", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"subsection1", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"subsection2", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"subsection3", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"link_text", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""some text""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"someFunc()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""some text 2""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"link_text3", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""some""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"link_text5", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class::Func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class::cls::func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class.Func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class.Func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class::Func(double,int)", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"func(double, int)", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class::Func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class::Func(int, double, cls::f)", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"func()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class1", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class2", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Class3", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"match", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\ref", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\refitem", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some_name", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\secreflist", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endsecreflist", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\subpage", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"intro", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\subpage", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"advanced", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""Advanced usage""", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\tableofcontents", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\section", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"sec", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"An example section", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\section", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\section", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\subsection", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"sec_2", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\subsubsection", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"sec", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"An example section", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\paragraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"sec", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"An example section", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\subparagraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"subpara", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"An example subparagraph", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\subsubparagraph", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"subsubpara", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"An example subsubparagraph", ClassificationEnum.Title),


        // --- Commands for displaying examples ---

        new Utils.FormattedFragmentText(@"\dontinclude", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dontinclude{lineno}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{doc}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{lineno}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp  ", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{doc}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"""some dir\include_test.cpp""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{local}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"""some dir\include_test.cpp""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{strip}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{nostrip}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{raise = 1 }", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{prefix = some great.prefix}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{lineno,local}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{doc,local}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{local,lineno, }", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{ local, doc }",  ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{}",  ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\include", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\includelineno", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\includedoc", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"include_test.cpp", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\line", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"example();", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\skip", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"main", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\skipline", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Include_Test t;", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\until", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{doc}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Adding a resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{lineno}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{doc}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{trimleft}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{local}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{strip}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{nostrip}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{raise=0}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{prefix=fn_}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{prefix = some prefix , doc}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{lineno,local}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{doc,local}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{trimleft,local}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{local,lineno}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{local ,doc}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{ local, trimleft  }", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{local,}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{doc,  }", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{  }", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\snippet", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\snippetlineno", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"snippets/example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"\snippetdoc", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"Some resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippetdoc", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{doc}", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippetdoc", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{ raise =5 }", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippetdoc", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"{prefix=pref, raise=5 }", ClassificationEnum.ParameterClamped),
        new Utils.FormattedFragmentText(@"example.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"resource", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\snippetdoc", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\verbinclude", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some dir\include_test.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\htmlinclude", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some dir\html.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\htmlinclude[block]", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"html.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\latexinclude", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some dir\tex.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\rtfinclude", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some dir\rtf.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\maninclude", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some dir\man.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\docbookinclude", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some dir\doc.cpp", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\xmlinclude", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"some dir\xml.cpp", ClassificationEnum.Parameter1),


        // --- Commands for visual enhancements ---

        new Utils.FormattedFragmentText(@"\a", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.EmphasisMinor),
        new Utils.FormattedFragmentText(@"@a", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"y::p", ClassificationEnum.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\b", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.EmphasisMajor),
        new Utils.FormattedFragmentText(@"@b", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"y::p", ClassificationEnum.EmphasisMajor),

        new Utils.FormattedFragmentText(@"\c", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"@c", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"y::p", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"@p", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"y::p", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"::thing", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\p", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"thing", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\arg", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\li", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\code{.py}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\code{.c}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\code{.cpp}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\code{.c++}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\code{.unparsed}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\code", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endcode", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"@copydoc", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"MyClass::myfunction(type1,type2)", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"@copydoc", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"MyClass::myfunction()", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\brief", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\copybrief", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"foo()", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\details", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\copydetails", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"foo()", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"\docbookonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\enddocbookonly", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"width=2\textwidth", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""foo""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"height=\textwidth", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\dot", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\enddot", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\emoji", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@":smile:", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\emoji", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"left_luggage", ClassificationEnum.Parameter2),

        new Utils.FormattedFragmentText(@"@msc", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\msc", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\endmsc", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\startuml", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@startuml{myimage.png}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""Image Caption""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"@startuml{json, myimage.png}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""Image Caption""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"@startuml{json}", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"@enduml", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"filename", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""file name""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""foo  test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"filename", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""file name""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""file name""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"filename", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""foo test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"\dotfile", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\mscfile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"file_name.msc", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""test""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\diafile", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"""path\with space\file_name.dia""", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\doxyconfig", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"PROJECT_NAME", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\e", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"really", ClassificationEnum.EmphasisMinor),
        new Utils.FormattedFragmentText(@"\em", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"x", ClassificationEnum.EmphasisMinor),

        new Utils.FormattedFragmentText(@"\htmlonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\htmlonly[block]", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endhtmlonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\latexonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endlatexonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\manonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endmanonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\rtfonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endrtfonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\verbatim", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endverbatim", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\xmlonly", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\endxmlonly", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f(", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f)", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f[", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f]", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\f{eqnarray*}{", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f{eqnarray*}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\f}", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"html", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"html", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"application.jpg", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"latex", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"application.eps", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"""My application""", ClassificationEnum.Title),
        new Utils.FormattedFragmentText(@"width=10cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"docbook", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""file name.eps""", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"width=200cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\image{inline,anchor:id}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"rtf", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"""path with space/name.rtf""", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\image{inline}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"xml", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"file.xml", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"height=1cm", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\n", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\n", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\n", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\n", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\@", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@@", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\~", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\~english", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\~", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\~english", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\~dutch", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\~german", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\~", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\&", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\$", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\#", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\<", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\>", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\%", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\.", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\=", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\::", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\|", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\--", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\---", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\---", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\{", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@{", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@}", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("\\\"", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("@\"", ClassificationEnum.Command),

        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command), // \\cite exc
        new Utils.FormattedFragmentText(@"@\", ClassificationEnum.Command), // @\cite exc
        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\cite", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"label", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"@\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\cite", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"label", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"@\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\\", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"\section", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"cmdthrows", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText(@"\\cite label (the ""\\cite label"" should be a parameter of \section)", ClassificationEnum.Title),


        // --- Additional stuff ---

        new Utils.FormattedFragmentText(@"\ingroup", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"foo  ", ClassificationEnum.Parameter1),

      };

      return expectedTextFragments;
    }


    [TestMethod()]
    public void VariousKeywordsShouldBeFormatted_CRLF()
    {
      var input = Utils.ReadTestInputFromFile("VariousKeywords.cpp");
      Assert.IsTrue(Regex.Matches(input, "\r\n").Count > 10); // Cross-check the input file

      var expectedTextFragments = GetExpectedTextFragmentsForVariousKeywordsTests();
      var actualTextFragments = Utils.ConvertToTextFragments(input, Utils.CreateDefaultCommentParser().Parse(input));

      // Write fragments to file for easy checking of test failures.
      Utils.WriteFragmentsToFile("VariousKeywords_Expected.txt", expectedTextFragments);
      Utils.WriteFragmentsToFile("VariousKeywords_Actual.txt", actualTextFragments);

      CollectionAssert.AreEqual(expectedTextFragments, actualTextFragments);
    }


    [TestMethod()]
    public void VariousKeywordsShouldBeFormatted_LF()
    {
      var input = Utils.ReadTestInputFromFile("VariousKeywords.cpp");
      input = input.Replace("\r\n", "\n");
      Assert.IsTrue(Regex.Matches(input, "\r").Count == 0); // Cross-check

      var expectedTextFragments = GetExpectedTextFragmentsForVariousKeywordsTests();
      var actualTextFragments = Utils.ConvertToTextFragments(input, Utils.CreateDefaultCommentParser().Parse(input));

      CollectionAssert.AreEqual(expectedTextFragments, actualTextFragments);
    }


    [TestMethod()]
    public void CasesWhereNothingShouldBeFormatted()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragments = formatter.Parse(
        Utils.ReadTestInputFromFile("NothingToFormat.cpp"));
      Assert.AreEqual(0, actualFragments.Count());
    }


    [TestMethod()]
    public void SingleStarShouldFormatItalic()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragmentGroups = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_SingleStar.cpp"));

      var expectedFragments = GetExpectationsForItalic();
      CollectionAssert.AreEqual(expectedFragments, Utils.ToFlatFragmentList(actualFragmentGroups));
    }


    [TestMethod()]
    public void SingleUnderscoreShouldFormatItalic()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragmentGroups = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_SingleStar.cpp"));

      var expectedFragments = GetExpectationsForItalic();
      CollectionAssert.AreEqual(expectedFragments, Utils.ToFlatFragmentList(actualFragmentGroups));
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
      var actualFragmentGroups = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_DoubleStar.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(ClassificationEnum.EmphasisMajor);
      CollectionAssert.AreEqual(expectedFragments, Utils.ToFlatFragmentList(actualFragmentGroups));
    }


    [TestMethod()]
    public void DoubleUnderscoreShouldFormatBold()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragmentGroups = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_DoubleUnderscore.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(ClassificationEnum.EmphasisMajor);
      CollectionAssert.AreEqual(expectedFragments, Utils.ToFlatFragmentList(actualFragmentGroups));
    }


    [TestMethod()]
    public void DoubleTildeShouldFormatStrikethrough()
    {
      var formatter = Utils.CreateDefaultCommentParser();
      var actualFragmentGroups = formatter.Parse(
        Utils.ReadTestInputFromFile("Markdown_DoubleTilde.cpp"));

      var expectedFragments = GetExpectationsForBoldOrStrikethrough(ClassificationEnum.Strikethrough);
      CollectionAssert.AreEqual(expectedFragments, Utils.ToFlatFragmentList(actualFragmentGroups));
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
      var actualFragmentGroups = formatter.Parse(
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

      CollectionAssert.AreEqual(expectedFragments, Utils.ToFlatFragmentList(actualFragmentGroups));
    }


    [TestMethod()]
    public void UnicodeParametersShouldWork()
    {
      var input = Utils.ReadTestInputFromFile("UnicodeParametersUTF8.cpp");
      var actualFragmentGroups = Utils.CreateDefaultCommentParser().Parse(input);

      var expectedTextFragments = new List<Utils.FormattedFragmentText>() {
        new Utils.FormattedFragmentText("@param", ClassificationEnum.Command),
        new Utils.FormattedFragmentText("t\U0001F600t", ClassificationEnum.Parameter1),

        new Utils.FormattedFragmentText(@"\image", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"latex", ClassificationEnum.Parameter1),
        new Utils.FormattedFragmentText("\"file \U0001F600 name.eps\"", ClassificationEnum.Parameter2),
        new Utils.FormattedFragmentText("\"test\U0001F600\"", ClassificationEnum.Title),

        new Utils.FormattedFragmentText("**te\U0001F600st**", ClassificationEnum.EmphasisMajor),
      };

      var actualTextFragments = Utils.ConvertToTextFragments(input, actualFragmentGroups);
      CollectionAssert.AreEqual(expectedTextFragments, actualTextFragments);
    }


    [TestMethod()]
    public void IfFragmentsOverlapTheFirstOneShouldWin()
    {
      var input = Utils.ReadTestInputFromFile("OverlappingHighlights.cpp");
      var actualFragmentGroups = Utils.CreateDefaultCommentParser().Parse(input);

      var expectedTextFragments = new List<Utils.FormattedFragmentText>() {
        new Utils.FormattedFragmentText(@"`backtics @b should win`", ClassificationEnum.InlineCode),
        new Utils.FormattedFragmentText(@"**bold `should win` over**", ClassificationEnum.EmphasisMajor),
        new Utils.FormattedFragmentText(@"`@par inline @b code should @ref win since it @a comes first`", ClassificationEnum.InlineCode),
        new Utils.FormattedFragmentText(@"*@par italic @b should @ref win since it @a comes first*", ClassificationEnum.EmphasisMinor),

        new Utils.FormattedFragmentText(@"@mainpage", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Some `inline`, **bold** and *italic* text should loose to titles", ClassificationEnum.Title),
        
        new Utils.FormattedFragmentText(@"@par", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"`inline` text at the start should also loose to titles", ClassificationEnum.Title),

        new Utils.FormattedFragmentText(@"@par", ClassificationEnum.Command),
        new Utils.FormattedFragmentText(@"Some other @b cmd should @ref loose to @a title", ClassificationEnum.Title),
      };

      var actualTextFragments = Utils.ConvertToTextFragments(input, actualFragmentGroups);
      CollectionAssert.AreEqual(expectedTextFragments, actualTextFragments);
    }

  }
}
