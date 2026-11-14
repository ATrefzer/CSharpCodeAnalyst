using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class CallClassificationParseTests
{
    [TestCase("F()", "Tools", "F", RelationshipAttribute.IsStaticCall)]
    [TestCase("Tools.F()", "Tools", "F", RelationshipAttribute.IsStaticCall)]
    [TestCase("Demo.Tools.F()", "Tools", "F", RelationshipAttribute.IsStaticCall)]
    [TestCase("global::Demo.Tools.F()", "Tools", "F", RelationshipAttribute.IsStaticCall)]
    [TestCase("Alias.F()", "Tools", "F", RelationshipAttribute.IsStaticCall)]
    [TestCase("G<int>.F()", "G<T>", "F", RelationshipAttribute.IsStaticCall)]
    [TestCase("Demo.G<int>.F()", "G<T>", "F", RelationshipAttribute.IsStaticCall)]
    [TestCase("ClosedAlias.F()", "G<T>", "F", RelationshipAttribute.IsStaticCall)]
    [TestCase("Generic<int>()", "Tools", "Generic<T>", RelationshipAttribute.IsStaticCall)]
    [TestCase("Demo.Tools.Generic<int>()", "Tools", "Generic<T>", RelationshipAttribute.IsStaticCall)]
    [TestCase("I()", "Base", "I", RelationshipAttribute.None)]
    [TestCase("this.I()", "Base", "I", RelationshipAttribute.IsThisCall)]
    [TestCase("base.I()", "Base", "I", RelationshipAttribute.IsBaseCall)]
    [TestCase("other.I()", "Base", "I", RelationshipAttribute.IsInstanceCall)]
    [TestCase("other?.I()", "Base", "I", RelationshipAttribute.IsInstanceCall)]
    [TestCase("GenericInstance<int>()", "Base", "GenericInstance<T>", RelationshipAttribute.None)]
    [TestCase("other.Ext()", "Extensions", "Ext", RelationshipAttribute.IsExtensionMethodCall)]
    [TestCase("other?.Ext()", "Extensions", "Ext", RelationshipAttribute.IsExtensionMethodCall)]
    [TestCase("Extensions.Ext(other)", "Extensions", "Ext", RelationshipAttribute.IsExtensionMethodCall)]
    public async Task Invocation_UsesSemanticDispatchKind(string expression, string targetType, string targetMethod,
        RelationshipAttribute expected)
    {
        var graph = await Parse(expression + ";");
        var caller = graph.Nodes.Values.Single(n => n.Name == "Caller");
        var calls = caller.Relationships.Where(r => r.Type == RelationshipType.Calls).ToArray();
        Assert.That(calls, Has.Length.EqualTo(1));
        var target = graph.Nodes[calls[0].TargetId];
        Assert.That(target.Name, Is.EqualTo(targetMethod));
        Assert.That(target.Parent!.Name, Is.EqualTo(targetType));
        Assert.That(calls[0].Attributes, Is.EqualTo(expected));
        Assert.That(calls[0].SourceLocations, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DeferredStaticInvocation_RemainsUsesWithoutCallFlags()
    {
        var graph = await Parse("System.Action action = () => Demo.G<int>.F();");
        var caller = graph.Nodes.Values.Single(n => n.Name == "Caller");
        var references = caller.Relationships.Where(r => graph.Nodes[r.TargetId].Name == "F").ToArray();
        Assert.That(references, Has.Length.EqualTo(1));
        Assert.That(references[0].Type, Is.EqualTo(RelationshipType.Uses));
        Assert.That(references[0].Attributes, Is.EqualTo(RelationshipAttribute.None));
        Assert.That(caller.Relationships.Where(r => r.Type == RelationshipType.Calls), Is.Empty);
    }

    private static async Task<CodeGraph> Parse(string body)
    {
        var code = $$"""
            using static Demo.Tools;
            using Alias = Demo.Tools;
            using ClosedAlias = Demo.G<int>;
            namespace Demo;
            static class Tools { public static void F() { } public static void Generic<T>() { } }
            class G<T> { public static void F() { } }
            class Base { public void I() { } public void GenericInstance<T>() { } }
            static class Extensions { public static void Ext(this Base value) { } }
            class Subject : Base { void Caller(Subject other) { {{body}} } }
            """;
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        var graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        return graph;
    }
}
