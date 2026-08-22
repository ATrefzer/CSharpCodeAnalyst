using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A destructor and a static constructor reach the graph as ordinary methods named "Finalize" and
///     ".cctor" - the Roslyn symbol names. The dead code analysis drops them because no code can
///     reference either, the runtime calls them. What identifies them is their
///     <see cref="MemberRole" />, not their name (see <see cref="MemberRoleParseTests" />); the names
///     are still asserted here because saved graphs from before the role rely on them.
/// </summary>
[TestFixture]
public class RuntimeInvokedMemberParseTests
{
    [OneTimeSetUp]
    public async Task ParseCode()
    {
        const string code = """
                            namespace Demo;

                            public class Widget
                            {
                                static Widget() { }

                                ~Widget() { }

                                public void Used() { }
                            }

                            public class User
                            {
                                public void Run(Widget widget)
                                {
                                    widget.Used();
                                }
                            }
                            """;

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(code);
        _graph = result.CodeGraph;
    }

    private CodeGraph _graph = null!;

    [Test]
    public void DestructorAndStaticConstructor_CarryTheRoslynSymbolNames()
    {
        var methods = _graph.Nodes.Values
            .Where(n => n is { ElementType: CodeElementType.Method, Parent.Name: "Widget" })
            .Select(n => n.Name)
            .ToList();

        Assert.That(methods, Is.SupersetOf(new[] { ".cctor", "Finalize" }));
    }

    [Test]
    public void DeadCodeAnalysis_DoesNotReportThem()
    {
        var reported = DeadCodeAnalysis.Calculate(_graph).Select(f => f.Element.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(reported, Does.Not.Contain(".cctor"));
            Assert.That(reported, Does.Not.Contain("Finalize"));
        });
    }
}
