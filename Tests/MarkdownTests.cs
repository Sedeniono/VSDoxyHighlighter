using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
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
    public void SimpleCasesWithNoFragments()
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
    public void StandardNonNestedCases()
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
    public void NestedButMerged()
    {
      DoTest("This *__is some__* test.", ("*__is some__*", ClassificationEnum.EmphasisHuge));
      DoTest("This __*is some*__ test.", ("__*is some*__", ClassificationEnum.EmphasisHuge));
      DoTest("This **_is some_** test.", ("**_is some_**", ClassificationEnum.EmphasisHuge));
      DoTest("This _**is some**_ test.", ("_**is some**_", ClassificationEnum.EmphasisHuge));

      DoTest("This *_is some_* test.", ("*_is some_*", ClassificationEnum.EmphasisMinor));
      DoTest("This _*is some*_ test.", ("_*is some*_", ClassificationEnum.EmphasisMinor));
      DoTest("This *is _some_ test.*", ("*is _some_ test.*", ClassificationEnum.EmphasisMinor));
      DoTest("This _is *some* test._", ("_is *some* test._", ClassificationEnum.EmphasisMinor));

      DoTest("This **__is some__** test.", ("**__is some__**", ClassificationEnum.EmphasisMajor));
      DoTest("This __**is some**__ test.", ("__**is some**__", ClassificationEnum.EmphasisMajor));
      DoTest("This **is __some__ test.**", ("**is __some__ test.**", ClassificationEnum.EmphasisMajor));
      DoTest("This __is **some** test.__", ("__is **some** test.__", ClassificationEnum.EmphasisMajor));

      // This is parsed as "*is *some*" since a space before the ending * is not allowed.
      DoTest("This *is *some* test.*", ("*is *some*", ClassificationEnum.EmphasisMinor));
      DoTest("This _is _some_ test._", ("_is _some_", ClassificationEnum.EmphasisMinor));
      DoTest("This **is **some** test.**", ("**is **some**", ClassificationEnum.EmphasisMajor));
      DoTest("This __is __some__ test.__", ("__is __some__", ClassificationEnum.EmphasisMajor));

      DoTest("This _***is some***_ test.", ("_***is some***_", ClassificationEnum.EmphasisHuge));
      DoTest("This __***is some***__ test.", ("__***is some***__", ClassificationEnum.EmphasisHuge));
      DoTest("This ___***is some***___ test.", ("___***is some***___", ClassificationEnum.EmphasisHuge));
      DoTest("This *___is some___* test.", ("*___is some___*", ClassificationEnum.EmphasisHuge));
      DoTest("This **___is some___** test.", ("**___is some___**", ClassificationEnum.EmphasisHuge));
      DoTest("This ***___is some___*** test.", ("***___is some___***", ClassificationEnum.EmphasisHuge));
    }


    [TestMethod()]
    public void NestedWithIncreasingLevel()
    {
      DoTest("This **is *some* test.**", 
        ("**is ", ClassificationEnum.EmphasisMajor),
        ("*some*", ClassificationEnum.EmphasisHuge),
        (" test.**", ClassificationEnum.EmphasisMajor));
      DoTest("This **is _some_ test.**",
        ("**is ", ClassificationEnum.EmphasisMajor),
        ("_some_", ClassificationEnum.EmphasisHuge),
        (" test.**", ClassificationEnum.EmphasisMajor));

      DoTest("This __is *some* test.__",
        ("__is ", ClassificationEnum.EmphasisMajor),
        ("*some*", ClassificationEnum.EmphasisHuge),
        (" test.__", ClassificationEnum.EmphasisMajor));
      DoTest("This __is _some_ test.__",
        ("__is ", ClassificationEnum.EmphasisMajor),
        ("_some_", ClassificationEnum.EmphasisHuge),
        (" test.__", ClassificationEnum.EmphasisMajor));

      DoTest("This *is __some__ test.*",
        ("*is ", ClassificationEnum.EmphasisMinor),
        ("__some__", ClassificationEnum.EmphasisHuge),
        (" test.*", ClassificationEnum.EmphasisMinor));
      DoTest("This *is **some** test.*",
        ("*is ", ClassificationEnum.EmphasisMinor),
        ("**some**", ClassificationEnum.EmphasisHuge),
        (" test.*", ClassificationEnum.EmphasisMinor));
      DoTest("This *is **some** test.", ("**some**", ClassificationEnum.EmphasisMajor));

      DoTest("This _is **some** test._",
        ("_is ", ClassificationEnum.EmphasisMinor),
        ("**some**", ClassificationEnum.EmphasisHuge),
        (" test._", ClassificationEnum.EmphasisMinor));
      DoTest("This _is __some__ test._",
        ("_is ", ClassificationEnum.EmphasisMinor),
        ("__some__", ClassificationEnum.EmphasisHuge),
        (" test._", ClassificationEnum.EmphasisMinor));
      DoTest("This _is __some__ test.", ("__some__", ClassificationEnum.EmphasisMajor));
    }


    [TestMethod()]
    public void ContainingInvalidEmphasis()
    {
      // Four or more successive markers are not recognized by Doxygen.
      DoTest("****Some tests****");
      DoTest("****Some _more_ tests****", ("_more_", ClassificationEnum.EmphasisMinor));
      DoTest("****Some __more__ tests****", ("__more__", ClassificationEnum.EmphasisMajor));
      DoTest("****Some ___more___ tests****", ("___more___", ClassificationEnum.EmphasisHuge));
      DoTest("****Some ____more____ tests****");
      DoTest("****Some *more* tests****", ("*more*", ClassificationEnum.EmphasisMinor));
      DoTest("****Some **more** tests****", ("**more**", ClassificationEnum.EmphasisMajor));
      DoTest("****Some ***more*** tests****", ("***more***", ClassificationEnum.EmphasisHuge));
      DoTest("****Some ****more**** tests****");

      DoTest("____Some tests____");
      DoTest("____Some *more* tests____", ("*more*", ClassificationEnum.EmphasisMinor));
      DoTest("____Some **more** tests____", ("**more**", ClassificationEnum.EmphasisMajor));
      DoTest("____Some ***more*** tests____", ("***more***", ClassificationEnum.EmphasisHuge));
      DoTest("____Some ****more**** tests____");
      DoTest("____Some _more_ tests____", ("_more_", ClassificationEnum.EmphasisMinor));
      DoTest("____Some __more__ tests____", ("__more__", ClassificationEnum.EmphasisMajor));
      DoTest("____Some ___more___ tests____", ("___more___", ClassificationEnum.EmphasisHuge));
      DoTest("____Some ____more____ tests____");

      DoTest("*****Some tests*****");
      DoTest("_____Some tests_____");
    }


    [TestMethod()]
    public void SurroundingCharactersAtMarkers()
    {
      DoTest("(*Some*) test", ("*Some*", ClassificationEnum.EmphasisMinor));
      DoTest(":*Some*: test", ("*Some*", ClassificationEnum.EmphasisMinor));
      DoTest("<*Some*> test", ("*Some*", ClassificationEnum.EmphasisMinor));

      DoTest("S*ome* test");
      DoTest("1*Some* test");
      DoTest(".*Some* test");

      DoTest("* Some* test");
      DoTest("*1Some* test", ("*1Some*", ClassificationEnum.EmphasisMinor));
      DoTest("*-Some* test", ("*-Some*", ClassificationEnum.EmphasisMinor));
      DoTest("*(Some* test", ("*(Some*", ClassificationEnum.EmphasisMinor));

      DoTest("*Some * test");
      DoTest("*Some(* test");
      DoTest("*Some-* test");
      DoTest("*Some1* test", ("*Some1*", ClassificationEnum.EmphasisMinor));

      DoTest("*Some*test");
      DoTest("*Some*1test");
      DoTest("*Some*:test", ("*Some*", ClassificationEnum.EmphasisMinor));

      // The "_" do not mark an emphasis because the match ends with the second "*".
      DoTest("*_Some test*_", ("*_Some test*", ClassificationEnum.EmphasisMinor));
      DoTest("_*Some test_*", ("_*Some test_", ClassificationEnum.EmphasisMinor));
      DoTest("*_*Some test*_*", ("*_*Some test*", ClassificationEnum.EmphasisMinor));
      DoTest("_*_Some test_*_", ("_*_Some test_", ClassificationEnum.EmphasisMinor));
    }



  }
}

