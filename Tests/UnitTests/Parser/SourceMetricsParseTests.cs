using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

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

                                    public void CoalesceAssign(string? s)
                                    {
                                        s ??= "x";
                                    }

                                    public bool PatternCombinator(int x)
                                    {
                                        return x is > 0 and < 100;
                                    }

                                    public int SwitchExprDefault(int x)
                                    {
                                        return x switch
                                        {
                                            1 => 10,
                                            _ when x < 0 => -1,
                                            _ => 0
                                        };
                                    }
                                }

                                abstract class Abstract
                                {
                                    public abstract void NoBody();
                                }
                                """;

    private static CSharpCodeAnalyst.CodeParser.Parser.Parser CreateParser()
    {
        return new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
    }

    [Test]
    public async Task Metrics_AreComputed()
    {
        var parser = CreateParser();
        var result = await parser.ParseSourceAsync(Code);

        var bar = MetricsFor(result, "Bar");
        var lines = MetricsFor(result, "Lines");
        var complex = MetricsFor(result, "Complex");
        var coalesceAssign = MetricsFor(result, "CoalesceAssign");
        var patternCombinator = MetricsFor(result, "PatternCombinator");
        var switchExprDefault = MetricsFor(result, "SwitchExprDefault");

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

            // 1 + '??=' = 2.
            Assert.That(coalesceAssign.CyclomaticComplexity, Is.EqualTo(2));

            // 1 + 'and' pattern combinator = 2.
            Assert.That(patternCombinator.CyclomaticComplexity, Is.EqualTo(2));

            // 1 + case '1' + guarded '_ when' arm = 3; the bare '_' catch-all does not count,
            // matching how a classic 'default:' label is excluded.
            Assert.That(switchExprDefault.CyclomaticComplexity, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Metrics_NotComputed_ForMemberWithoutBody()
    {
        var parser = CreateParser();
        var result = await parser.ParseSourceAsync(Code);

        var noBody = result.CodeGraph.Nodes.Values.Single(
            n => n.ElementType == CodeElementType.Method && n.Name == "NoBody");

        Assert.That(result.Metrics.TryGet(noBody.Id), Is.Null);
    }

    private static MemberMetrics MetricsFor(ParseResult result, string methodName)
    {
        var method = result.CodeGraph.Nodes.Values.Single(n => n.ElementType == CodeElementType.Method && n.Name == methodName);
        var metrics = result.Metrics.TryGet(method.Id);
        Assert.That(metrics, Is.Not.Null, $"No metrics for {methodName}");
        return metrics!;
    }
}
