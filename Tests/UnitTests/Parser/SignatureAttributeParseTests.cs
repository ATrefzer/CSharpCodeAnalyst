using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class SignatureAttributeParseTests
{
    private const string Mark = "[Mark(typeof(Token), Kind = typeof(Token))]";

    [TestCase("class Subject { void Run(ATTR int value) { } }", "Run")]
    [TestCase("class Subject { RETURN int Run() => 1; }", "Run")]
    [TestCase("class Subject { public Subject(ATTR int value) { } }", ".ctor")]
    [TestCase("delegate void Callback(ATTR int value);", "Callback")]
    [TestCase("RETURN delegate int Callback();", "Callback")]
    [TestCase("class Subject { public int this[ATTR int index] => 0; }", "this[]")]
    [TestCase("class Subject { public int Value { RETURN get => 1; } }", "get_Value")]
    [TestCase("class Subject { public int Value { PARAM set { } } }", "set_Value")]
    [TestCase("partial class Subject { partial void Run(ATTR int value); partial void Run(int value) { } }", "Run")]
    [TestCase("record Subject(ATTR int Value);", ".ctor")]
    public async Task SignatureAttribute_RecordsDependenciesWithoutDecoratingOwner(string declaration, string ownerName)
    {
        var graph = await Parse(declaration.Replace("ATTR", Mark)
            .Replace("RETURN", Mark.Replace("[Mark", "[return: Mark"))
            .Replace("PARAM", Mark.Replace("[Mark", "[param: Mark")));
        var owner = FindOwner(graph, ownerName);
        AssertDependencies(graph, owner, 1);
        Assert.That(owner.Attributes, Does.Not.Contain("MarkAttribute"),
            "A parameter/return annotation is a dependency, not an annotation on its owning element.");
    }

    [Test]
    public async Task ParameterAndReturnSites_KeepBothLocations()
    {
        var graph = await Parse($$"""
            class Subject
            {
                {{Mark.Replace("[Mark", "[return: Mark")}}
                int Run({{Mark}} int value) => value;
            }
            """);
        AssertDependencies(graph, FindOwner(graph, "Run"), 2);
    }

    [Test]
    public async Task OwnAttribute_StillAppearsInMetadata()
    {
        var graph = await Parse($"class Subject {{ {Mark} void Run() {{ }} }}");
        var owner = FindOwner(graph, "Run");
        AssertDependencies(graph, owner, 1);
        Assert.That(owner.Attributes, Does.Contain("MarkAttribute"));
    }

    private static CodeElement FindOwner(CodeGraph graph, string name)
    {
        return graph.Nodes.Values.Single(n => n.Name == name && (name != ".ctor" || n.Parent!.Name == "Subject"));
    }

    private static void AssertDependencies(CodeGraph graph, CodeElement owner, int siteCount)
    {
        var attribute = graph.Nodes.Values.Single(n => n.Name == "MarkAttribute");
        var edge = owner.Relationships.Single(r => r.Type == RelationshipType.UsesAttribute && r.TargetId == attribute.Id);
        Assert.That(edge.SourceLocations, Has.Count.EqualTo(siteCount));
        Assert.That(owner.Relationships.Where(r => r.Type == RelationshipType.Uses).Select(r => graph.Nodes[r.TargetId].Name),
            Does.Contain("Token"));
        var callTargets = owner.Relationships.Where(r => r.Type == RelationshipType.Calls).Select(r => graph.Nodes[r.TargetId]).ToArray();
        Assert.That(callTargets.Any(n => n.Name == ".ctor" && n.Parent!.Name == "MarkAttribute"), Is.True);
        Assert.That(callTargets.Select(n => n.Name), Does.Contain("set_Kind"));
        Assert.That(callTargets.Select(n => n.Name), Does.Not.Contain("get_Kind"));
    }

    private static async Task<CodeGraph> Parse(string declaration)
    {
        var code = $$"""
            [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
            class MarkAttribute : System.Attribute
            {
                public MarkAttribute(System.Type type) { }
                public System.Type Kind { get; set; }
            }
            class Token { }
            {{declaration}}
            """;
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        var graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        return graph;
    }
}
