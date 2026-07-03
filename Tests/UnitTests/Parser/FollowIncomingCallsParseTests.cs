using CodeGraph.Graph;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Virtual/override/base calls and a constructor calling an inherited method, within one snippet.
///     Verifies the parsed call edges (self-recursion, base calls, ctor -> inherited Build). Self-contained
///     probe - migrated from the former FollowingIncomingCalls approval fixture (which, despite its name,
///     only asserted parse output; the explorer heuristic itself is covered elsewhere).
/// </summary>
[TestFixture]
public class FollowIncomingCallsParseTests
{
    private const string Code = """
                                namespace Demo;

                                public class Base
                                {
                                    private Base? _base;

                                    public virtual void AddToSlave()
                                    {
                                        _base?.AddToSlave();
                                    }

                                    public void Build()
                                    {
                                        AddToSlave();
                                    }
                                }

                                public class ViewModelAdapter1 : Base
                                {
                                    public override void AddToSlave()
                                    {
                                        base.AddToSlave();
                                    }
                                }

                                public class ViewModelAdapter2 : Base
                                {
                                    public override void AddToSlave()
                                    {
                                        base.AddToSlave();
                                    }
                                }

                                public class Driver
                                {
                                    private readonly ViewModelAdapter1 _adapter1;
                                    private readonly ViewModelAdapter2 _adapter2;

                                    public Driver()
                                    {
                                        _adapter1 = new ViewModelAdapter1();
                                        _adapter2 = new ViewModelAdapter2();
                                        _adapter1.Build();
                                    }
                                }
                                """;

    private CodeGraph.Graph.CodeGraph _graph = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        var parser = new CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(Code);
        _graph = result.CodeGraph;
    }

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(NamesOf(CodeElementType.Class),
            Is.EquivalentTo(new[] { "Base", "Driver", "ViewModelAdapter1", "ViewModelAdapter2" }));
    }

    [Test]
    public void MethodCalls_AreDetected()
    {
        var expected = new[]
        {
            "Base.AddToSlave -> Base.AddToSlave",
            "Base.Build -> Base.AddToSlave",
            "ViewModelAdapter1.AddToSlave -> Base.AddToSlave",
            "ViewModelAdapter2.AddToSlave -> Base.AddToSlave",
            "Driver..ctor -> Base.Build"
        };

        Assert.That(AllCalls(), Is.EquivalentTo(expected));
    }

    private string[] AllCalls()
    {
        return _graph.Nodes.Values
            .SelectMany(source => source.Relationships
                .Where(r => r.Type == RelationshipType.Calls)
                .Select(r => $"{Label(source)} -> {Label(_graph.Nodes[r.TargetId])}"))
            .ToArray();
    }

    private static string Label(CodeElement element)
    {
        return $"{element.Parent?.Name}.{element.Name}";
    }

    private string[] NamesOf(CodeElementType type)
    {
        return _graph.Nodes.Values.Where(n => n.ElementType == type).Select(n => n.Name).ToArray();
    }
}
