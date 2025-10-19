using CSharpCodeAnalyst.Common;

namespace CodeParserTests.Search;

[TestFixture]
public class PascalCaseSearchTests
{
    [Test]
    [TestCase("InvertBooleanConverter", true, true)]
    [TestCase("InverterBoxController", true, true)]
    [TestCase("SomeOtherClass", true, false)]
    [TestCase("Invert123Boolean456Converter", true, true)]
    public void PascalCaseSearchTest(string searchInput, bool isPascalCaseExpected, bool isMatch)
    {
        const string searchTerm = "InvBoC";
        var (isPascalCase, regex) = PascalCaseSearch.CreateSearchRegex(searchTerm);

        Assert.AreEqual(isPascalCaseExpected, isPascalCase);

        if (!isPascalCaseExpected || regex == null)
        {
            return;
        }

        Assert.AreEqual(isMatch, regex.IsMatch(searchInput));
    }
}