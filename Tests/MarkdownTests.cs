using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace VSDoxyHighlighter.Tests
{
  [TestClass()]
  public class FragmentsMatcherMarkdownEmphasisAndStrikethroughTests
  {
    private static List<Utils.FormattedFragmentText> FindFragments(string input)
    {
      var m = new FragmentsMatcherMarkdownEmphasisAndStrikethrough();
      var foundGroups = m.FindFragments(input);
      return Utils.ConvertToTextFragments(input, foundGroups);
    }


    private static void DoTest(string input, params (string text, ClassificationEnum classification)[] expectedFragments)
    {
      var found = FindFragments(input);
      var expected = expectedFragments.Select(
        f => new Utils.FormattedFragmentText(f.text, f.classification)).ToList();

      var expectedStr = expectedFragments.Any() 
        ? string.Join(", ", expected.Select(f => $"[\"{f.Text}\", {f.Classification}]")) 
        : "[]";
      var foundStr = found.Any() 
        ? string.Join(", ", found.Select(f => $"[\"{f.Text}\", {f.Classification}]")) 
        : "[]";
      var message = $"\nExpected: {expectedStr}\nFound   : {foundStr}";
      
      CollectionAssert.AreEqual(expected, found, message);
    }


    [TestMethod()]
    public void ExpectNoFragments()
    {
      DoTest("");

      DoTest("Plain text without any emphasis.");
      DoTest("Single ~strikethrough~ does not exist.");

      DoTest("Imbalanced *emphasis marker.");
      DoTest("Imbalanced _emphasis marker.");
      DoTest("Imbalanced **emphasis marker.");
      DoTest("Imbalanced __emphasis marker.");
      DoTest("Imbalanced ~~strikethrough marker.");

      DoTest("Imbalanced emphasis* marker.");
      DoTest("Imbalanced emphasis_ marker.");
      DoTest("Imbalanced emphasis** marker.");
      DoTest("Imbalanced emphasis__ marker.");
      DoTest("Imbalanced strikethrough~ marker.");

      DoTest("Line with *line \n break*.");
      DoTest("Line with _line \n break_.");
      DoTest("Line with **line \n break**.");
      DoTest("Line with __line \n break__.");
      DoTest("Line with ~~line \n break~~.");
    }


    [TestMethod()]
    public void NonNestedCases()
    {
      DoTest("This *is some* test.", ("*is some*", ClassificationEnum.EmphasisMinor));
      DoTest("This _is some_ test.", ("_is some_", ClassificationEnum.EmphasisMinor));
      DoTest("This **is some** test.", ("**is some**", ClassificationEnum.EmphasisMajor));
      DoTest("This __is some__ test.", ("__is some__", ClassificationEnum.EmphasisMajor));
      DoTest("This ***is some*** test.", ("***is some***", ClassificationEnum.EmphasisHuge));
      DoTest("This ___is some___ test.", ("___is some___", ClassificationEnum.EmphasisHuge));
      DoTest("This ~~is some~~ test.", ("~~is some~~", ClassificationEnum.Strikethrough));

      DoTest("*At start* of line.", ("*At start*", ClassificationEnum.EmphasisMinor));
      DoTest("_At start_ of line.", ("_At start_", ClassificationEnum.EmphasisMinor));
      DoTest("**At start** of line.", ("**At start**", ClassificationEnum.EmphasisMajor));
      DoTest("__At start__ of line.", ("__At start__", ClassificationEnum.EmphasisMajor));
      DoTest("***At start*** of line.", ("***At start***", ClassificationEnum.EmphasisHuge));
      DoTest("___At start___ of line.", ("___At start___", ClassificationEnum.EmphasisHuge));
      DoTest("~~At start~~ of line.", ("~~At start~~", ClassificationEnum.Strikethrough));

      DoTest("At *end of line.*", ("*end of line.*", ClassificationEnum.EmphasisMinor));
      DoTest("At _end of line._", ("_end of line._", ClassificationEnum.EmphasisMinor));
      DoTest("At **end of line.**", ("**end of line.**", ClassificationEnum.EmphasisMajor));
      DoTest("At __end of line.__", ("__end of line.__", ClassificationEnum.EmphasisMajor));
      DoTest("At ***end of line.***", ("***end of line.***", ClassificationEnum.EmphasisHuge));
      DoTest("At ___end of line.___", ("___end of line.___", ClassificationEnum.EmphasisHuge));
      DoTest("At ~~end of line.~~", ("~~end of line.~~", ClassificationEnum.Strikethrough));
    }


    [TestMethod()]
    public void SuccessiveCombinations()
    {
      DoTest("This *__is some__* test.", ("*__is some__*", ClassificationEnum.EmphasisHuge));
      DoTest("This __*is some*__ test.", ("__*is some*__", ClassificationEnum.EmphasisHuge));
      DoTest("This **_is some_** test.", ("**_is some_**", ClassificationEnum.EmphasisHuge));
      DoTest("This _**is some**_ test.", ("_**is some**_", ClassificationEnum.EmphasisHuge));
    }

    //[TestMethod()]
    //public void TTTTT()
    //{
    //  Assert.IsTrue(FindFragments("Text without any emphasis.").Count == 0);

    //  DoTest("This **is some** test.",
    //    ("**is some**", ClassificationEnum.EmphasisMajor)
    //  );

    //  DoTest("This **is _some_ test** test.",
    //    ("**is ", ClassificationEnum.EmphasisMajor),
    //    ("_some_", ClassificationEnum.EmphasisMinor),
    //    (" test**", ClassificationEnum.EmphasisMajor)
    //  );
    //}
  }
}

