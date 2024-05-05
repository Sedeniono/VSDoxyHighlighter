using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VSDoxyHighlighter.Tests
{
  [TestClass()]
  public class SemanticsUtilitiesTests
  {
    private string ExtractTypeOfParam(string s)
    { 
      return SemanticsUtilities.ExtractTypeOfParam(s);
    }


    [TestMethod()]
    public void TestExtractTypeOfParam()
    {
      Assert.AreEqual(null, ExtractTypeOfParam(null));
      Assert.AreEqual(null, ExtractTypeOfParam(@""));

      Assert.AreEqual(@"int", ExtractTypeOfParam(@"void func( int "));
      Assert.AreEqual(@"const int", ExtractTypeOfParam(@"void func(const int "));
      Assert.AreEqual(@"long double", ExtractTypeOfParam(@"void func(const int p1, long double"));
      Assert.AreEqual(@"std::function<void(int, double)>", ExtractTypeOfParam(@"  std::function<void(int, double)>  "));
      Assert.AreEqual(@"TemplatedType<int, double, short>", ExtractTypeOfParam(@"func(double p1, TemplatedType<int, double, short> "));
      Assert.AreEqual(@"auto", ExtractTypeOfParam(@"func(double p1, TemplatedType<int, double, short> p2, auto"));
      Assert.AreEqual(@"decltype([](double volatile &, int){return false;})", ExtractTypeOfParam(@"decltype([](double volatile &, int){return false;})"));
      Assert.AreEqual(@"int (*param5)(double, short)", ExtractTypeOfParam(@", int (*param5)(double, short)"));
      Assert.AreEqual(@"auto", ExtractTypeOfParam(@"double, short> p2, auto "));

      // Cases where the type is clearly incomplete.
      Assert.AreEqual(null, ExtractTypeOfParam(@"double, short> "));
      Assert.AreEqual(null, ExtractTypeOfParam(@"  int, double)  "));
    }
  }
}

