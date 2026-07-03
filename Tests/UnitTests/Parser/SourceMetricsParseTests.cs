using CodeGraph.Graph;
using CodeGraph.Metrics;
using CodeParser.Parser;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class SourceMetricsParseTests
{
    private const string Code = """
                                namespace Demo;

                                class C
                                {
                                    /// <summary>Doc.</summary>
                                    public int Bar(int x) => x * 2;

                                    // leading comment
                                    public void Lines()
                                    {
                                        var a = 1; // trailing comment
                                        var b = 2;

                                        var c = 3;
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
        var result = await parser.ParseSourceAsync(Code);

        Assert.That(result.Metrics.IsEmpty, Is.True);
    }

    [Test]
    public async Task Metrics_AreComputed()
    {
        var parser = CreateParser(true);
        var result = await parser.ParseSourceAsync(Code);

        var bar = MetricsFor(result, "Bar");
        var lines = MetricsFor(result, "Lines");
        var complex = MetricsFor(result, "Complex");

        Assert.Multiple(() =>
        {
            // Expression-bodied one-liner, with a doc comment above.
            Assert.That(bar.CodeLines, Is.EqualTo(1));
            Assert.That(bar.CommentLines, Is.EqualTo(1), "The /// doc comment counts");
            Assert.That(bar.LogicalLinesOfCode, Is.EqualTo(1), "Expression body counts as one");
            Assert.That(bar.CyclomaticComplexity, Is.EqualTo(1));

            // Declaration + '{' + three statements + '}' = 6 code lines; the blank line is not counted.
            Assert.That(lines.CodeLines, Is.EqualTo(6));
            // Only the leading '// comment'; the trailing comment sits on a code line.
            Assert.That(lines.CommentLines, Is.EqualTo(1));
            Assert.That(lines.LogicalLinesOfCode, Is.EqualTo(3));
            Assert.That(lines.CyclomaticComplexity, Is.EqualTo(1));

            // if + return + for + return = 4 statements (blocks excluded).
            Assert.That(complex.LogicalLinesOfCode, Is.EqualTo(4));
            // 1 + if + '&&' + for + '?:' = 5.
            Assert.That(complex.CyclomaticComplexity, Is.EqualTo(5));
            Assert.That(complex.CommentLines, Is.EqualTo(0));
        });
    }

    private static MemberMetrics MetricsFor(ParseResult result, string methodName)
    {
        var method = result.CodeGraph.Nodes.Values.Single(n => n.ElementType == CodeElementType.Method && n.Name == methodName);
        var metrics = result.Metrics.TryGet(method.Id);
        Assert.That(metrics, Is.Not.Null, $"No metrics for {methodName}");
        return metrics!;
    }
}
