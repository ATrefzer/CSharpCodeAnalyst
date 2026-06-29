using CodeGraph.Graph;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A constructor referenced only inside a lambda must not look unused. The lambda walker records a
///     "Uses" edge to the constructor (deferred reference), mirroring how it already treats method calls -
///     and unlike a direct object creation, which is a "Calls".
/// </summary>
[TestFixture]
public class LambdaConstructorReferenceTests
{
    private const string Code = """
                                namespace Demo;

                                public class Factory
                                {
                                    public Factory(int x) { }
                                }

                                public class Consumer
                                {
                                    public void BuildInLambda()
                                    {
                                        System.Func<int, Factory> make = i => new Factory(i);
                                    }

                                    public void BuildDirect()
                                    {
                                        var f = new Factory(1);
                                    }
                                }
                                """;

    private CodeGraph.Graph.CodeGraph _graph = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        _graph = Parse(Code);
    }

    [Test]
    public void ConstructorReferencedInLambda_GetsUsesEdge_NotCalls()
    {
        var ctor = Ctor("Factory");
        var buildInLambda = Method("BuildInLambda");

        Assert.Multiple(() =>
        {
            Assert.That(Has(buildInLambda, ctor, RelationshipType.Uses), Is.True,
                "The lambda should reference the constructor with a Uses edge.");
            Assert.That(Has(buildInLambda, ctor, RelationshipType.Calls), Is.False,
                "The lambda must not produce a Calls edge (we do not know when it runs).");
        });
    }

    [Test]
    public void DirectObjectCreation_StillGetsCallsEdge()
    {
        // Contrast: the same "new Factory(...)" in a normal body is a Calls.
        var ctor = Ctor("Factory");
        var buildDirect = Method("BuildDirect");

        Assert.That(Has(buildDirect, ctor, RelationshipType.Calls), Is.True,
            "A direct object creation is a Calls to the constructor.");
    }

    [Test]
    public void LambdaStillRecordsTheTypeUsage()
    {
        // The type "Uses" edge that already existed must remain.
        var factory = _graph.Nodes.Values.Single(n => n.ElementType == CodeElementType.Class && n.Name == "Factory");
        var buildInLambda = Method("BuildInLambda");

        Assert.That(Has(buildInLambda, factory, RelationshipType.Uses), Is.True);
    }

    private CodeElement Ctor(string containingType)
    {
        return _graph.Nodes.Values.Single(n =>
            n.ElementType == CodeElementType.Method && n.Name == ".ctor" && n.Parent?.Name == containingType);
    }

    private CodeElement Method(string name)
    {
        return _graph.Nodes.Values.Single(n => n.ElementType == CodeElementType.Method && n.Name == name);
    }

    private static bool Has(CodeElement source, CodeElement target, RelationshipType type)
    {
        return source.Relationships.Any(r => r.TargetId == target.Id && r.Type == type);
    }

    private static CodeGraph.Graph.CodeGraph Parse(string code)
    {
        var parser = new CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false));
        return parser.ParseSourceAsync(code).GetAwaiter().GetResult();
    }
}
