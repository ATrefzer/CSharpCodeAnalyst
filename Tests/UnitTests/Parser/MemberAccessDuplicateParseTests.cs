using CodeGraph.Graph;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Regression: a member access (obj.Prop) must be recorded once, not twice - it used to be processed
///     both as a MemberAccessExpression and as the nested IdentifierName, producing duplicate source
///     locations for the same line. Self-contained probe - migrated from the former MemberAccessDuplicate
///     approval fixture.
/// </summary>
[TestFixture]
public class MemberAccessDuplicateParseTests
{
    private const string Code = """
                                namespace Demo;

                                public class SearchGraphSource
                                {
                                    public SearchNode OriginalElement { get; set; }
                                }

                                public class SearchNode
                                {
                                    public string Name { get; set; }
                                }

                                public class MemberAccessDuplicate
                                {
                                    public void TestMethod()
                                    {
                                        var searchGraphSource = new SearchGraphSource();
                                        var proxySource = searchGraphSource.OriginalElement;
                                        var elementName = searchGraphSource.OriginalElement.Name;
                                    }
                                }
                                """;

    private CodeGraph.Graph.CodeGraph _graph = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        // Split off to mirror the original parser configuration of this scenario.
        var parser = new CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false));
        _graph = parser.ParseSourceAsync(Code).GetAwaiter().GetResult().CodeGraph;
    }

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(NamesOf(CodeElementType.Class),
            Is.EquivalentTo(new[] { "MemberAccessDuplicate", "SearchGraphSource", "SearchNode" }));
    }

    [Test]
    public void PropertyAccessChain_IsRecorded()
    {
        var testMethod = Node("TestMethod", CodeElementType.Method);

        var callTargets = testMethod.Relationships
            .Where(r => r.Type == RelationshipType.Calls)
            .Select(r => _graph.Nodes[r.TargetId].Name);

        Assert.That(callTargets, Is.EquivalentTo(new[] { "OriginalElement", "Name" }));
    }

    [Test]
    public void MemberAccess_HasNoDuplicateSourceLocationsPerLine()
    {
        var testMethod = Node("TestMethod", CodeElementType.Method);

        // OriginalElement is accessed on two different lines; each line must contribute exactly one location
        // (the bug produced two for the same line via double processing).
        var toOriginalElement = testMethod.Relationships
            .Single(r => r.Type == RelationshipType.Calls && _graph.Nodes[r.TargetId].Name == "OriginalElement");

        foreach (var lineGroup in toOriginalElement.SourceLocations.GroupBy(l => l.Line))
        {
            Assert.That(lineGroup.Count(), Is.EqualTo(1),
                $"Duplicate source locations on line {lineGroup.Key} (columns {string.Join(", ", lineGroup.Select(l => l.Column))}).");
        }
    }

    private string[] NamesOf(CodeElementType type)
    {
        return _graph.Nodes.Values.Where(n => n.ElementType == type).Select(n => n.Name).ToArray();
    }

    private CodeElement Node(string name, CodeElementType type)
    {
        return _graph.Nodes.Values.Single(n => n.Name == name && n.ElementType == type);
    }
}
