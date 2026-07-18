using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Persistence.Dto;

namespace CodeParserTests.UnitTests.Persistence;

[TestFixture]
public class ProjectDataTests
{
    private static CodeGraph RoundTrip(CodeGraph graph)
    {
        var projectData = new ProjectData();
        projectData.SetCodeGraph(graph);
        return projectData.GetCodeGraph();
    }

    [Test]
    public void RoundTrip_KeepsIsExternal()
    {
        // IsExternal was not part of the persisted element, so a saved and reloaded project came back with
        // the whole framework counted as the solution's own code: it fed the type graph behind the system
        // metrics, the architectural rules, and showed up as extra rows in the DSM matrix.
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("Asm");
        var ns = graph.CreateNamespace("Ns", assembly);
        graph.CreateClass("Internal", ns);
        graph.CreateExternalClass("External", ns);

        var restored = RoundTrip(graph);

        Assert.Multiple(() =>
        {
            Assert.That(restored.Nodes["Internal"].IsExternal, Is.False);
            Assert.That(restored.Nodes["External"].IsExternal, Is.True);
        });
    }

    [Test]
    public void RoundTrip_KeepsTheHierarchyAndTheRelationships()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("Asm");
        var ns = graph.CreateNamespace("Ns", assembly);
        var a = graph.CreateClass("A", ns);
        var b = graph.CreateClass("B", ns);
        a.Relationships.Add(new Relationship(a.Id, b.Id, RelationshipType.Uses));

        var restored = RoundTrip(graph);

        Assert.Multiple(() =>
        {
            Assert.That(restored.Nodes.Count, Is.EqualTo(4));
            Assert.That(restored.Nodes["A"].Parent!.Id, Is.EqualTo("Ns"));
            Assert.That(restored.Nodes["Ns"].Parent!.Id, Is.EqualTo("Asm"));
            Assert.That(restored.Nodes["A"].Relationships.Single().TargetId, Is.EqualTo("B"));
        });
    }
}
