using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class LambdaResultConversionParseTests
{
    [TestCase("System.Func<Target>", "() => 1", true)]
    [TestCase("System.Func<int, Target>", "x => x", true)]
    [TestCase("System.Func<int, Target>", "(int x) => x", true)]
    [TestCase("System.Func<Target>", "() => { return 1; }", true)]
    [TestCase("System.Func<Target>", "delegate { return 1; }", true)]
    [TestCase("System.Func<System.Func<Target>>", "() => () => 1", true)]
    [TestCase("System.Func<int, System.Func<int, Target>>", "x => y => y", true)]
    [TestCase("System.Func<System.Func<Target>>", "() => { return () => 1; }", true)]
    [TestCase("System.Func<System.Threading.Tasks.Task<Target>>", "async () => await System.Threading.Tasks.Task.FromResult(1)", true)]
    [TestCase("System.Func<Target>", "() => (Target)1", true)]
    [TestCase("System.Func<long>", "() => 1", false)]
    [TestCase("System.Action", "() => Helper.Value()", false)]
    public async Task LambdaResult_RecordsOnlyDeferredUserDefinedConversions(string delegateType, string lambda, bool expectsConversion)
    {
        var graph = await Parse($$"""
            class Target { public static implicit operator Target(int value) => new Target(); }
            class Helper { public static int Value() => 1; }
            class Subject { void M() { {{delegateType}} callback = {{lambda}}; } }
            """);
        var method = graph.Nodes.Values.Single(n => n.Name == "M");
        AssertConversion(graph, method, expectsConversion);
    }

    [Test]
    public async Task LambdaInEventInitializer_KeepsConversionOnEvent()
    {
        var graph = await Parse("""
            class Target { public static implicit operator Target(int value) => new Target(); }
            class Subject { public event System.Func<Target> Produce = () => 1; }
            """);
        AssertConversion(graph, graph.Nodes.Values.Single(n => n.Name == "Produce"), true);
    }

    private static void AssertConversion(CodeGraph graph, CodeElement source, bool expectsConversion)
    {
        var conversion = graph.Nodes.Values.Single(n => n.Name == "op_Implicit");
        var edges = source.Relationships.Where(r => r.TargetId == conversion.Id).ToArray();
        Assert.That(edges, Has.Length.EqualTo(expectsConversion ? 1 : 0));
        if (expectsConversion)
        {
            Assert.That(edges[0].Type, Is.EqualTo(RelationshipType.Uses));
            Assert.That(edges[0].SourceLocations, Has.Count.EqualTo(1));
        }

        Assert.That(source.Relationships.Where(r => r.Type == RelationshipType.Calls), Is.Empty,
            "A lambda result conversion is deferred, like the rest of its body.");
    }

    private static async Task<CodeGraph> Parse(string code)
    {
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        var graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        return graph;
    }
}
