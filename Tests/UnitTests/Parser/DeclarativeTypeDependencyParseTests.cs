using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class DeclarativeTypeDependencyParseTests
{
    [TestCase("where T : IMarker", "IMarker")]
    [TestCase("where T : Base", "Base")]
    [TestCase("where T : IGeneric<Token>", "IGeneric<T>,Token")]
    [TestCase("where T : Base, IMarker, new()", "Base,IMarker")]
    [TestCase("where T : class, new()", "")]
    public async Task DelegateConstraints_UseExistingTypeEdges(string constraints, string expected)
    {
        var graph = await Parse($$"""
            interface IMarker { }
            interface IGeneric<T> { }
            class Base { }
            class Token { }
            delegate void Callback<T>() {{constraints}};
            """);
        AssertUses(graph, graph.Nodes.Values.Single(n => n.Name == "Callback<T>"), expected);
    }

    [TestCase("void Local(Token value) { }", "Token", false)]
    [TestCase("Token Local() => null;", "Token", false)]
    [TestCase("void Local<T>() where T : IMarker { }", "IMarker", false)]
    [TestCase("void Local<T>() where T : IGeneric<Token> { }", "IGeneric<T>,Token", false)]
    [TestCase("void Local(Token[] values) { }", "Token", false)]
    [TestCase("Token Local(Token value) => value;", "Token", true)]
    [TestCase("void Outer() { void Local(Token value) { } }", "Token", false)]
    [TestCase("void Local<T>() where T : class, new() { }", "", false)]
    public async Task LocalSignatures_AggregateOnOwningMember(string declaration, string expected, bool inLambda)
    {
        var body = inLambda ? $"System.Action action = () => {{ {declaration} }};" : declaration;
        var graph = await Parse($$"""
            class Token { }
            interface IMarker { }
            interface IGeneric<T> { }
            class Subject { void Run() { {{body}} } }
            """);
        AssertUses(graph, graph.Nodes.Values.Single(n => n.Name == "Run"), expected);
        Assert.That(graph.Nodes.Values.Where(n => n.Name.StartsWith("Local") || n.Name == "Outer"), Is.Empty,
            "Local functions remain aggregated; signature coverage must not introduce new graph nodes.");
    }

    private static void AssertUses(CodeGraph graph, CodeElement owner, string expected)
    {
        var edges = owner.Relationships.ToArray();
        Assert.That(edges.All(r => r.Type == RelationshipType.Uses), Is.True);
        Assert.That(edges.Select(r => graph.Nodes[r.TargetId].Name),
            Is.EquivalentTo(expected.Split(',', StringSplitOptions.RemoveEmptyEntries)));
        Assert.That(edges.All(r => r.SourceLocations.Count > 0), Is.True);
    }

    private static async Task<CodeGraph> Parse(string code)
    {
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        var graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        return graph;
    }
}
