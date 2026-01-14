using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
      DoTest("Imbalanced ~strikethrough marker.");
      DoTest("Imbalanced ~~strikethrough marker.");

      DoTest("Imbalanced emphasis* marker.");
      DoTest("Imbalanced emphasis_ marker.");
      DoTest("Imbalanced emphasis** marker.");
      DoTest("Imbalanced emphasis__ marker.");
      DoTest("Imbalanced strikethrough~ marker.");
      DoTest("Imbalanced strikethrough~~ marker.");

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
    public void NestedStrikethroughAndStars()
    {
      DoTest("~~*Some test*~~", ("~~*Some test*~~", ClassificationEnum.StrikethroughEmphasisMinor));
      DoTest("~~**Some test**~~", ("~~**Some test**~~", ClassificationEnum.StrikethroughEmphasisMajor));
      DoTest("~~***Some test***~~", ("~~***Some test***~~", ClassificationEnum.StrikethroughEmphasisHuge));

      // Doxygen has somewhat peculiar behavior, compare comment in FindAndMarkInvalidEmphasisSpans().
      DoTest("*~~This is nothing~~*");
      DoTest("*Some ~~thing~~*", 
        ("*Some ", ClassificationEnum.EmphasisMinor), 
        ("~~thing~~", ClassificationEnum.StrikethroughEmphasisMinor), 
        ("*", ClassificationEnum.EmphasisMinor));
      DoTest("*~~Not a~~ thing*");
      DoTest("*This ~~is~~ something*",
        ("*This ", ClassificationEnum.EmphasisMinor),
        ("~~is~~", ClassificationEnum.StrikethroughEmphasisMinor),
        (" something*", ClassificationEnum.EmphasisMinor));
      DoTest("*~~Not a~~ test ~~thing~~ again*", 
        ("~~thing~~", ClassificationEnum.Strikethrough));
      DoTest("*Some **~~This is nothing~~** test*",
        ("*Some **~~This is nothing~~** test*", ClassificationEnum.EmphasisMinor));

      DoTest("**~~This is nothing~~**");
      DoTest("**Some ~~thing~~**",
        ("**Some ", ClassificationEnum.EmphasisMajor),
        ("~~thing~~", ClassificationEnum.StrikethroughEmphasisMajor),
        ("**", ClassificationEnum.EmphasisMajor));
      DoTest("**~~Not a~~ thing**");
      DoTest("**This ~~is~~ something**",
        ("**This ", ClassificationEnum.EmphasisMajor),
        ("~~is~~", ClassificationEnum.StrikethroughEmphasisMajor),
        (" something**", ClassificationEnum.EmphasisMajor));
      DoTest("**~~Not a~~ test ~~thing~~ again**",
        ("~~thing~~", ClassificationEnum.Strikethrough));
      DoTest("**Some *~~This is nothing~~* test**",
        ("**Some *~~This is nothing~~* test**", ClassificationEnum.EmphasisMajor));

      // In contrast to "*" and "**", Doxygen recognizes "***" here.
      DoTest("***~~This is something~~***",
        ("***~~This is something~~***", ClassificationEnum.StrikethroughEmphasisHuge));
      DoTest("***Some ~~thing~~***",
        ("***Some ", ClassificationEnum.EmphasisHuge),
        ("~~thing~~", ClassificationEnum.StrikethroughEmphasisHuge),
        ("***", ClassificationEnum.EmphasisHuge));
      DoTest("***~~Some~~ thing***",
        ("***", ClassificationEnum.EmphasisHuge),
        ("~~Some~~", ClassificationEnum.StrikethroughEmphasisHuge),
        (" thing***", ClassificationEnum.EmphasisHuge));
      DoTest("***This ~~is~~ something***",
        ("***This ", ClassificationEnum.EmphasisHuge),
        ("~~is~~", ClassificationEnum.StrikethroughEmphasisHuge),
        (" something***", ClassificationEnum.EmphasisHuge));
      DoTest("***~~This is a~~ test ~~thing~~ again***",
        ("***", ClassificationEnum.EmphasisHuge),
        ("~~This is a~~", ClassificationEnum.StrikethroughEmphasisHuge),
        (" test ", ClassificationEnum.EmphasisHuge),
        ("~~thing~~", ClassificationEnum.StrikethroughEmphasisHuge),
        (" again***", ClassificationEnum.EmphasisHuge));
      DoTest("***Some *~~This is nothing~~* test***",
        ("***Some *~~This is nothing~~* test***", ClassificationEnum.EmphasisHuge));
    }


    [TestMethod()]
    public void NestedStrikethroughAndUnderline()
    {
      DoTest("~~_Some test_~~", ("~~_Some test_~~", ClassificationEnum.StrikethroughEmphasisMinor));
      DoTest("~~__Some test__~~", ("~~__Some test__~~", ClassificationEnum.StrikethroughEmphasisMajor));
      DoTest("~~___Some test___~~", ("~~___Some test___~~", ClassificationEnum.StrikethroughEmphasisHuge));

      // Doxygen has somewhat peculiar behavior, compare comment in FindAndMarkInvalidEmphasisSpans().
      DoTest("_~~This is nothing~~_");
      DoTest("_Some ~~thing~~_",
        ("_Some ", ClassificationEnum.EmphasisMinor),
        ("~~thing~~", ClassificationEnum.StrikethroughEmphasisMinor),
        ("_", ClassificationEnum.EmphasisMinor));
      DoTest("_~~Not a~~ thing_");
      DoTest("_This ~~is~~ something_",
        ("_This ", ClassificationEnum.EmphasisMinor),
        ("~~is~~", ClassificationEnum.StrikethroughEmphasisMinor),
        (" something_", ClassificationEnum.EmphasisMinor));
      DoTest("_~~Not a~~ test ~~thing~~ again_",
        ("~~thing~~", ClassificationEnum.Strikethrough));
      DoTest("_Some __~~This is nothing~~__ test_",
        ("_Some __~~This is nothing~~__ test_", ClassificationEnum.EmphasisMinor));

      DoTest("__~~This is nothing~~__");
      DoTest("__Some ~~thing~~__",
        ("__Some ", ClassificationEnum.EmphasisMajor),
        ("~~thing~~", ClassificationEnum.StrikethroughEmphasisMajor),
        ("__", ClassificationEnum.EmphasisMajor));
      DoTest("__~~Not a~~ thing__");
      DoTest("__This ~~is~~ something__",
        ("__This ", ClassificationEnum.EmphasisMajor),
        ("~~is~~", ClassificationEnum.StrikethroughEmphasisMajor),
        (" something__", ClassificationEnum.EmphasisMajor));
      DoTest("__~~Not a~~ test ~~thing~~ again__",
        ("~~thing~~", ClassificationEnum.Strikethrough));
      DoTest("__Some _~~This is nothing~~_ test__",
        ("__Some _~~This is nothing~~_ test__", ClassificationEnum.EmphasisMajor));

      // In contrast to "_" and "__", Doxygen recognizes "___" here.
      DoTest("___~~This is something~~___",
        ("___~~This is something~~___", ClassificationEnum.StrikethroughEmphasisHuge));
      DoTest("___Some ~~thing~~___",
        ("___Some ", ClassificationEnum.EmphasisHuge),
        ("~~thing~~", ClassificationEnum.StrikethroughEmphasisHuge),
        ("___", ClassificationEnum.EmphasisHuge));
      DoTest("___~~Some~~ thing___",
        ("___", ClassificationEnum.EmphasisHuge),
        ("~~Some~~", ClassificationEnum.StrikethroughEmphasisHuge),
        (" thing___", ClassificationEnum.EmphasisHuge));
      DoTest("___This ~~is~~ something___",
        ("___This ", ClassificationEnum.EmphasisHuge),
        ("~~is~~", ClassificationEnum.StrikethroughEmphasisHuge),
        (" something___", ClassificationEnum.EmphasisHuge));
      DoTest("___~~This is a~~ test ~~thing~~ again___",
        ("___", ClassificationEnum.EmphasisHuge),
        ("~~This is a~~", ClassificationEnum.StrikethroughEmphasisHuge),
        (" test ", ClassificationEnum.EmphasisHuge),
        ("~~thing~~", ClassificationEnum.StrikethroughEmphasisHuge),
        (" again___", ClassificationEnum.EmphasisHuge));
      DoTest("___Some _~~This is nothing~~_ test___",
        ("___Some _~~This is nothing~~_ test___", ClassificationEnum.EmphasisHuge));
    }


    [TestMethod()]
    public void MultipleEquallyNested()
    {
      DoTest("*this **is** some ___nested___ test ***yet*** and __again__*",
        ("*this ", ClassificationEnum.EmphasisMinor),
        ("**is**", ClassificationEnum.EmphasisHuge),
        (" some ", ClassificationEnum.EmphasisMinor),
        ("___nested___", ClassificationEnum.EmphasisHuge),
        (" test ", ClassificationEnum.EmphasisMinor),
        ("***yet***", ClassificationEnum.EmphasisHuge),
        (" and ", ClassificationEnum.EmphasisMinor),
        ("__again__", ClassificationEnum.EmphasisHuge),
        ("*", ClassificationEnum.EmphasisMinor));

      DoTest("**this *is* some ___nested___ test ***yet*** and __again__**",
        ("**this ", ClassificationEnum.EmphasisMajor),
        ("*is*", ClassificationEnum.EmphasisHuge),
        (" some ", ClassificationEnum.EmphasisMajor),
        ("___nested___", ClassificationEnum.EmphasisHuge),
        (" test ", ClassificationEnum.EmphasisMajor),
        ("***yet***", ClassificationEnum.EmphasisHuge),
        (" and __again__**", ClassificationEnum.EmphasisMajor));

      DoTest("***this *is* some ___nested___ test **yet** and __again__***",
        ("***this *is* some ___nested___ test **yet** and __again__***", ClassificationEnum.EmphasisHuge));

      // Not actually nested at the start.
      DoTest("*this *is* some ___not nested___ test __beware__*",
        ("*this *is*", ClassificationEnum.EmphasisMinor),
        ("___not nested___", ClassificationEnum.EmphasisHuge),
        ("__beware__", ClassificationEnum.EmphasisMajor));

      DoTest("_this __is__ some ***nested*** test ___yet___ and **again**_",
        ("_this ", ClassificationEnum.EmphasisMinor),
        ("__is__", ClassificationEnum.EmphasisHuge),
        (" some ", ClassificationEnum.EmphasisMinor),
        ("***nested***", ClassificationEnum.EmphasisHuge),
        (" test ", ClassificationEnum.EmphasisMinor),
        ("___yet___", ClassificationEnum.EmphasisHuge),
        (" and ", ClassificationEnum.EmphasisMinor),
        ("**again**", ClassificationEnum.EmphasisHuge),
        ("_", ClassificationEnum.EmphasisMinor));

      DoTest("__this _is_ some ***nested*** test ___yet___ and **again**__",
        ("__this ", ClassificationEnum.EmphasisMajor),
        ("_is_", ClassificationEnum.EmphasisHuge),
        (" some ", ClassificationEnum.EmphasisMajor),
        ("***nested***", ClassificationEnum.EmphasisHuge),
        (" test ", ClassificationEnum.EmphasisMajor),
        ("___yet___", ClassificationEnum.EmphasisHuge),
        (" and **again**__", ClassificationEnum.EmphasisMajor));

      DoTest("___this _is_ some ***nested*** test __yet__ and **again**___",
        ("___this _is_ some ***nested*** test __yet__ and **again**___", ClassificationEnum.EmphasisHuge));

      // Not actually nested at the start.
      DoTest("_this _is_ some ***not nested*** test **beware**_",
        ("_this _is_", ClassificationEnum.EmphasisMinor),
        ("***not nested***", ClassificationEnum.EmphasisHuge),
        ("**beware**", ClassificationEnum.EmphasisMajor));
    }


    [TestMethod()]
    public void ContainingInvalidEmphasis()
    {
      // Four or more successive markers are not recognized by Doxygen.
      DoTest("****Some tests****");
      DoTest("*****Some tests*****");
      DoTest("****Some _more_ tests****", ("_more_", ClassificationEnum.EmphasisMinor));
      DoTest("****Some __more__ tests****", ("__more__", ClassificationEnum.EmphasisMajor));
      DoTest("****Some ___more___ tests****", ("___more___", ClassificationEnum.EmphasisHuge));
      DoTest("****Some ____more____ tests****");
      DoTest("****Some *more* tests****", ("*more*", ClassificationEnum.EmphasisMinor));
      DoTest("****Some **more** tests****", ("**more**", ClassificationEnum.EmphasisMajor));
      DoTest("****Some ***more*** tests****", ("***more***", ClassificationEnum.EmphasisHuge));
      DoTest("****Some ****more**** tests****");
      DoTest("*Some ****more**** tests*", ("*Some ****more**** tests*", ClassificationEnum.EmphasisMinor));
      DoTest("**Some ****more**** tests**", ("**Some ****more**** tests**", ClassificationEnum.EmphasisMajor));
      DoTest("***Some ****more**** tests***", ("***Some ****more**** tests***", ClassificationEnum.EmphasisHuge));
      DoTest("*Some ____more____ tests*", ("*Some ____more____ tests*", ClassificationEnum.EmphasisMinor));
      DoTest("**Some ____more____ tests**", ("**Some ____more____ tests**", ClassificationEnum.EmphasisMajor));
      DoTest("***Some ____more____ tests***", ("***Some ____more____ tests***", ClassificationEnum.EmphasisHuge));

      DoTest("____Some tests____");
      DoTest("_____Some tests_____");
      DoTest("____Some *more* tests____", ("*more*", ClassificationEnum.EmphasisMinor));
      DoTest("____Some **more** tests____", ("**more**", ClassificationEnum.EmphasisMajor));
      DoTest("____Some ***more*** tests____", ("***more***", ClassificationEnum.EmphasisHuge));
      DoTest("____Some ****more**** tests____");
      DoTest("____Some _more_ tests____", ("_more_", ClassificationEnum.EmphasisMinor));
      DoTest("____Some __more__ tests____", ("__more__", ClassificationEnum.EmphasisMajor));
      DoTest("____Some ___more___ tests____", ("___more___", ClassificationEnum.EmphasisHuge));
      DoTest("____Some ____more____ tests____");
      DoTest("_Some ____more____ tests_", ("_Some ____more____ tests_", ClassificationEnum.EmphasisMinor));
      DoTest("__Some ____more____ tests__", ("__Some ____more____ tests__", ClassificationEnum.EmphasisMajor));
      DoTest("___Some ____more____ tests___", ("___Some ____more____ tests___", ClassificationEnum.EmphasisHuge));
      DoTest("_Some ****more**** tests_", ("_Some ****more**** tests_", ClassificationEnum.EmphasisMinor));
      DoTest("__Some ****more**** tests__", ("__Some ****more**** tests__", ClassificationEnum.EmphasisMajor));
      DoTest("___Some ****more**** tests___", ("___Some ****more**** tests___", ClassificationEnum.EmphasisHuge));
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


    [TestMethod()]
    public void SameLevelNested()
    {
      DoTest("*Some *test* text*", ("*Some *test*", ClassificationEnum.EmphasisMinor));
      DoTest("**Some **test** text**", ("**Some **test**", ClassificationEnum.EmphasisMajor));
      DoTest("***Some ***test*** text***", ("***Some ***test***", ClassificationEnum.EmphasisHuge));
      DoTest("_Some _test_ text_", ("_Some _test_", ClassificationEnum.EmphasisMinor));
      DoTest("__Some __test__ text__", ("__Some __test__", ClassificationEnum.EmphasisMajor));
      DoTest("___Some ___test___ text___", ("___Some ___test___", ClassificationEnum.EmphasisHuge));
      DoTest("~~Some ~~test~~ text~~", ("~~Some ~~test~~", ClassificationEnum.Strikethrough));
    }


    [TestMethod()]
    public void MultipleSeparateBlocks()
    { 
      DoTest("This *is some* test with _multiple emphasis_ markers. **It ~~continues~~ *on* some** more!",
        ("*is some*", ClassificationEnum.EmphasisMinor),
        ("_multiple emphasis_", ClassificationEnum.EmphasisMinor),
        ("**It ", ClassificationEnum.EmphasisMajor),
        ("~~continues~~", ClassificationEnum.StrikethroughEmphasisMajor),
        (" ", ClassificationEnum.EmphasisMajor),
        ("*on*", ClassificationEnum.EmphasisHuge),
        (" some**", ClassificationEnum.EmphasisMajor));
    }
  }
}

