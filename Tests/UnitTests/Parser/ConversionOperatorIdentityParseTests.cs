using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class ConversionOperatorIdentityParseTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task SameSourceWithDifferentTargets_KeepsBothBodiesAndRoutesCalls(bool explicitConversion, bool reverseDeclarations)
    {
        var modifier = explicitConversion ? "explicit" : "implicit";
        var first = $"public static {modifier} operator First(Source value) => new First();";
        var second = $"public static {modifier} operator Second(Source value) => new Second();";
        var declarations = reverseDeclarations ? second + first : first + second;
        var firstValue = explicitConversion ? "(First)value" : "value";
        var secondValue = explicitConversion ? "(Second)value" : "value";
        var code = $$"""
                     class First { }
                     class Second { }
                     class Source { {{declarations}} }
                     class Caller
                     {
                         First ToFirst(Source value) => {{firstValue}};
                         Second ToSecond(Source value) => {{secondValue}};
                     }
                     """;
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        var graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        var operatorName = explicitConversion ? "op_Explicit" : "op_Implicit";
        var operators = graph.Nodes.Values.Where(n => n.Name == operatorName).ToArray();

        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        Assert.That(operators, Has.Length.EqualTo(2), "The return type distinguishes conversion operators with identical parameters.");

        foreach (var targetName in new[] { "First", "Second" })
        {
            var target = graph.Nodes.Values.Single(n => n.Name == targetName);
            var conversion = operators.Single(n => n.Relationships.Any(r => r.Type == RelationshipType.Creates && r.TargetId == target.Id));
            var caller = graph.Nodes.Values.Single(n => n.Name == "To" + targetName);
            Assert.That(caller.Relationships.Where(r => r.Type == RelationshipType.Calls).Select(r => r.TargetId),
                Is.EquivalentTo(new[] { conversion.Id }));
        }
    }
}
