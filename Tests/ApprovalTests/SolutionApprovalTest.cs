using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class SolutionApprovalTests : ApprovalTestBase
{

    [Test]
    public void Assemblies_ShouldBeDetected()
    {
        var assemblies = Graph.Nodes.Values.Where(n => n.ElementType == CodeElementType.Assembly)
            .Select(a => a.FullName).ToHashSet();

        var expected = new[]
        {
            "ModuleLevel0",
            "ModuleLevel1",
            "ModuleLevel2",
            "Old.CSharpLanguage"
        };
        Assert.That(assemblies, Is.EquivalentTo(expected));
    }
}