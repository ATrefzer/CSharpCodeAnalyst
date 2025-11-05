using CodeGraph.Graph;

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
            "Core.BasicLanguageFeatures",
            "Core.ObjectOriented",
            "Core.Generics",
            "Core.Events",
            "Core.Cycles",
            "Core.Namespaces",
            "Core.MethodGroups",
            "Regression.SpecificBugs",
            "OrderProcessingExample",
            "FollowHeuristic",
            "ModuleLevel0",
            "ModuleLevel1",
            "ModuleLevel2",
            "Old.CSharpLanguage"
        };
        CollectionAssert.AreEquivalent(expected, assemblies);
    }
}