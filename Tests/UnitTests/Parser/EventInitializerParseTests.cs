using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class EventInitializerParseTests
{
    [TestCase("Helper.Handle", false, RelationshipType.Uses, RelationshipAttribute.IsMethodGroup)]
    [TestCase("Helper.Handle", true, RelationshipType.Uses, RelationshipAttribute.IsMethodGroup)]
    [TestCase("() => Helper.Handle()", false, RelationshipType.Uses, RelationshipAttribute.None)]
    [TestCase("delegate { Helper.Handle(); }", false, RelationshipType.Uses, RelationshipAttribute.None)]
    [TestCase("Helper.Create()", false, RelationshipType.Calls, RelationshipAttribute.IsStaticCall)]
    public async Task Initializer_AnchorsReferencesOnEvent(string initializer, bool isStatic,
        RelationshipType expectedType, RelationshipAttribute expectedAttributes)
    {
        var graph = await Parse($$"""
            class Helper
            {
                public static void Handle() { }
                public static System.Action Create() => Handle;
            }
            class Subject { public {{(isStatic ? "static" : "")}} event System.Action Changed = {{initializer}}; }
            """);
        var eventElement = graph.Nodes.Values.Single(n => n.Name == "Changed");
        var target = graph.Nodes.Values.Single(n => n.Name == (initializer == "Helper.Create()" ? "Create" : "Handle"));
        var references = eventElement.Relationships.Where(r => r.TargetId == target.Id).ToArray();
        Assert.That(references, Has.Length.EqualTo(1));
        Assert.That(references[0].Type, Is.EqualTo(expectedType));
        Assert.That(references[0].Attributes, Is.EqualTo(expectedAttributes));
        Assert.That(references[0].SourceLocations, Is.Not.Empty);
        Assert.That(graph.Nodes.Values.SelectMany(n => n.Relationships).Where(r => r.Type == RelationshipType.Handles), Is.Empty,
            "Initializer analysis records references without introducing a separate event-handler association policy.");
    }

    [Test]
    public async Task MultipleDeclarators_KeepInitializersOnTheirOwnEvents()
    {
        var graph = await Parse("""
            class Subject
            {
                public event System.Action First = A, Second = B;
                static void A() { }
                static void B() { }
            }
            """);
        foreach (var (eventName, handlerName) in new[] { ("First", "A"), ("Second", "B") })
        {
            var eventElement = graph.Nodes.Values.Single(n => n.Name == eventName);
            var targets = eventElement.Relationships.Select(r => graph.Nodes[r.TargetId].Name).ToArray();
            Assert.That(targets, Is.EquivalentTo(new[] { handlerName }));
        }
    }

    [Test]
    public async Task ReceiverConstruction_IsImmediateButHandlerIsOnlyReferenced()
    {
        var graph = await Parse("""
            class Handler { public Handler() { } public void Handle() { } }
            class Subject { public event System.Action Changed = new Handler().Handle; }
            """);
        var eventElement = graph.Nodes.Values.Single(n => n.Name == "Changed");
        var edges = eventElement.Relationships.Select(r => (graph.Nodes[r.TargetId].Name, r.Type)).ToArray();
        Assert.That(edges, Is.EquivalentTo(new[]
        {
            ("Handler", RelationshipType.Creates),
            (".ctor", RelationshipType.Calls),
            ("Handle", RelationshipType.Uses)
        }));
    }

    [Test]
    public async Task EmptyInitializers_DoNotInventMemberReferences()
    {
        var graph = await Parse("""
            class Subject
            {
                public event System.Action Uninitialized;
                public event System.Action Empty = null;
            }
            """);
        Assert.That(graph.Nodes.Values.Where(n => n.ElementType == CodeElementType.Event)
            .SelectMany(n => n.Relationships), Is.Empty);
    }

    private static async Task<CodeGraph> Parse(string code)
    {
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        var graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        return graph;
    }
}
