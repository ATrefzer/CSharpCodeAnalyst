using CodeGraph.Graph;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Regression: an assignment to a property/field must be recorded once, not twice (it used to be
///     double-counted via both the assignment handler and AnalyzeIdentifier/AnalyzeMemberAccess), and a
///     relationship must not collect duplicate source locations for the same line.
///     Self-contained probe - migrated from the former AssignmentDuplicate approval fixture.
/// </summary>
[TestFixture]
public class AssignmentDuplicateParseTests
{
    private const string Code = """
                                namespace Demo;

                                public class AssignmentDuplicate
                                {
                                    public string TestField;
                                    public string TestProperty { get; set; }

                                    public void TestMethod()
                                    {
                                        TestProperty = "value";
                                        TestField = "value";
                                        TestProperty = "another value";
                                        TestProperty = TestField;
                                    }
                                }
                                """;

    private CodeGraph.Graph.CodeGraph _graph = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        // Split off to mirror the original parser configuration of this scenario.
        var parser = new CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(Code);
        _graph = result.CodeGraph;
    }

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(NamesOf(CodeElementType.Class), Is.EquivalentTo(new[] { "AssignmentDuplicate" }));
    }

    [Test]
    public void PropertyAndFieldAccess_AreRecorded()
    {
        var testMethod = Node("TestMethod", CodeElementType.Method);

        Assert.Multiple(() =>
        {
            Assert.That(Has(testMethod, Node("TestProperty", CodeElementType.Property), RelationshipType.Calls), Is.True);
            Assert.That(Has(testMethod, Node("TestField", CodeElementType.Field), RelationshipType.Uses), Is.True);
        });
    }

    [Test]
    public void TheOnlyCall_IsToTheProperty()
    {
        var testMethod = Node("TestMethod", CodeElementType.Method);

        var callTargets = testMethod.Relationships
            .Where(r => r.Type == RelationshipType.Calls)
            .Select(r => _graph.Nodes[r.TargetId].Name);

        Assert.That(callTargets, Is.EquivalentTo(new[] { "TestProperty" }));
    }

    [Test]
    public void Relationships_HaveNoDuplicateSourceLocationsPerLine()
    {
        var testMethod = Node("TestMethod", CodeElementType.Method);

        var property = Single(testMethod, "TestProperty", RelationshipType.Calls);
        var field = Single(testMethod, "TestField", RelationshipType.Uses);

        Assert.Multiple(() =>
        {
            AssertNoDuplicateLocationsPerLine(property);
            AssertNoDuplicateLocationsPerLine(field);
        });
    }

    private void AssertNoDuplicateLocationsPerLine(Relationship relationship)
    {
        foreach (var lineGroup in relationship.SourceLocations.GroupBy(l => l.Line))
        {
            Assert.That(lineGroup.Count(), Is.EqualTo(1),
                $"Duplicate source locations on line {lineGroup.Key} to {_graph.Nodes[relationship.TargetId].Name} " +
                $"(columns {string.Join(", ", lineGroup.Select(l => l.Column))}).");
        }
    }

    private Relationship Single(CodeElement source, string targetName, RelationshipType type)
    {
        return source.Relationships.Single(r => r.Type == type && _graph.Nodes[r.TargetId].Name == targetName);
    }

    private string[] NamesOf(CodeElementType type)
    {
        return _graph.Nodes.Values.Where(n => n.ElementType == type).Select(n => n.Name).ToArray();
    }

    private CodeElement Node(string name, CodeElementType type)
    {
        return _graph.Nodes.Values.Single(n => n.Name == name && n.ElementType == type);
    }

    private static bool Has(CodeElement source, CodeElement target, RelationshipType type)
    {
        return source.Relationships.Any(r => r.TargetId == target.Id && r.Type == type);
    }
}
