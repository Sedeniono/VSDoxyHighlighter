using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VSDoxyHighlighter.Tests
{
  [TestClass()]
  public class DoxygenCommandsTests
  {
    // The code essentially converts the DoxygenCommands.DefaultCommandGroups to DoxygenCommands.DefaultCommandsInConfig
    // and then back again to a list of groups. The result should be the same as the original DoxygenCommands.DefaultCommandGroups.
    [TestMethod()]
    public void RoundtripThroughDefaultConfigShouldResultInOriginalGroups()
    {
      var result = new DoxygenCommands(new GeneralOptionsFake()).CommandGroups;

      Assert.AreEqual(DoxygenCommands.DefaultCommandGroups.Length, result.Count);

      foreach (DoxygenCommandGroup expectedGroup in DoxygenCommands.DefaultCommandGroups) {
        int resultGroupIdx = result.FindIndex(group => group.Commands.Contains(expectedGroup.Commands[0]));
        Assert.IsTrue(resultGroupIdx >= 0);
        DoxygenCommandGroup resultGroup = result[resultGroupIdx];

        CollectionAssert.AreEquivalent(expectedGroup.Commands, resultGroup.Commands); // Same elements, order does not matter
        Assert.AreEqual(expectedGroup.DoxygenCommandType, resultGroup.DoxygenCommandType);
        Assert.AreEqual(expectedGroup.RegexCreator, resultGroup.RegexCreator);
        CollectionAssert.AreEqual(expectedGroup.FragmentTypes, resultGroup.FragmentTypes); // Same elements in the same order
      }
    }
  }
}
