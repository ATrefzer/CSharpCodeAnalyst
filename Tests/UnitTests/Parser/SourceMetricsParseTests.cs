using CodeGraph.Graph;
using CodeGraph.Metrics;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class SourceMetricsParseTests
{
    private const string Code = """
                                namespace Demo;

                                class C
                                {
                                    public int Bar(int x) => x * 2;

                                    public void Lines()
                                    {
                                        var a = 1;
                                        var b = 2;
                                    }

                                    public int Complex(int x, bool b)
                                    {
                                        if (x > 0 && b) { return 1; }
                                        for (int i = 0; i < x; i++) { }
                                        return b ? x : -x;
                                    }
                                }
                                """;

    private static CodeParser.Parser.Parser CreateParser(bool collectMetrics)
    {
        return new CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false, collectSourceMetrics: collectMetrics));
    }

    [Test]
    public async Task Metrics_NotCollected_WhenFlagIsOff()
    {
        var parser = CreateParser(false);
        await parser.ParseSourceAsync(Code);

        Assert.That(parser.Metrics.IsEmpty, Is.True);
    }

    [Test]
    public async Task Metrics_LinesOfCodeAndComplexity_AreComputed()
    {
        var parser = CreateParser(true);
        var graph = await parser.ParseSourceAsync(Code);

        var bar = MetricsFor(parser, graph, "Bar");
        var lines = MetricsFor(parser, graph, "Lines");
        var complex = MetricsFor(parser, graph, "Complex");

        Assert.Multiple(() =>
        {
            // Expression-bodied one-liner.
            Assert.That(bar.LinesOfCode, Is.EqualTo(1));
            Assert.That(bar.CyclomaticComplexity, Is.EqualTo(1));

            // Declaration + '{' + two statements + '}'.
            Assert.That(lines.LinesOfCode, Is.EqualTo(5));
            Assert.That(lines.CyclomaticComplexity, Is.EqualTo(1));

            // 1 (base) + if + '&&' + for + '?:' = 5.
            Assert.That(complex.CyclomaticComplexity, Is.EqualTo(5));
        });
    }

    private static MemberMetrics MetricsFor(CodeParser.Parser.Parser parser, CodeGraph.Graph.CodeGraph graph, string methodName)
    {
        var method = graph.Nodes.Values.Single(n => n.ElementType == CodeElementType.Method && n.Name == methodName);
        var metrics = parser.Metrics.TryGet(method.Id);
        Assert.That(metrics, Is.Not.Null, $"No metrics for {methodName}");
        return metrics!;
    }
}
