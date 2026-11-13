using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class TopLevelExternalRelationshipParseTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ExternalFirstUsedInTopLevelCode_IsPresentWithItsWholeHierarchy(bool alreadyUsedInMember)
    {
        var code = "System.Console.WriteLine(1);";
        if (alreadyUsedInMember)
        {
            code += "class Other { void M() => System.Console.WriteLine(2); }";
        }

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), true, false));
        var graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        var execute = graph.Nodes.Values.Single(n => n.Name == "Execute");

        foreach (var element in graph.Nodes.Values)
        {
            foreach (var edge in element.Relationships)
            {
                Assert.That(graph.Nodes.ContainsKey(edge.SourceId), Is.True);
                Assert.That(graph.Nodes.ContainsKey(edge.TargetId), Is.True, "Every edge must have a materialized target.");
            }

            if (element.Parent is { } parent)
            {
                Assert.That(graph.Nodes.GetValueOrDefault(parent.Id), Is.SameAs(parent));
                Assert.That(parent.Children, Does.Contain(element));
            }

            foreach (var child in element.Children)
            {
                Assert.That(graph.Nodes.GetValueOrDefault(child.Id), Is.SameAs(child));
                Assert.That(child.Parent, Is.SameAs(element));
            }
        }

        Assert.That(execute.Relationships.Any(r => r.Type == RelationshipType.Uses &&
            graph.Nodes[r.TargetId] is { Name: "Console", IsExternal: true }), Is.True);
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
    }

    [Test]
    public async Task ExcludedExternals_DoNotCreateDanglingEdges()
    {
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        var graph = (await parser.ParseSourceAsync("System.Console.WriteLine(1);")).CodeGraph;

        Assert.That(graph.Nodes.Values.Any(n => n.IsExternal), Is.False);
        Assert.That(graph.Nodes.Values.SelectMany(n => n.Relationships), Is.Empty);
        Assert.That(parser.Diagnostics.HasDiagnostics, Is.False);
    }
}
