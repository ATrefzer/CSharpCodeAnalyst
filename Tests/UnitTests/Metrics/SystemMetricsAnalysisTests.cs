using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Metrics;

[TestFixture]
public class SystemMetricsAnalysisTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _ns = _graph.CreateNamespace("N");
    }

    private TestCodeGraph _graph;
    private CodeElement _ns;

    private CodeElement Type(string name)
    {
        return _graph.CreateClass(name, _ns);
    }

    private static void Depend(CodeElement from, CodeElement to)
    {
        from.Relationships.Add(new Relationship(from.Id, to.Id, RelationshipType.Uses));
    }

    [Test]
    public void Chain_A_B_C_HasPropagationCostOneHalf()
    {
        // A -> B -> C. Reachable: A->{B,C}=2, B->{C}=1, C->{}=0 => 3 of 3*2=6 ordered pairs.
        var a = Type("A");
        var b = Type("B");
        var c = Type("C");
        Depend(a, b);
        Depend(b, c);

        var metrics = SystemMetricsAnalysis.Calculate(_graph);

        Assert.That(metrics.TypeCount, Is.EqualTo(3));
        Assert.That(metrics.TypeDependencyCount, Is.EqualTo(2));
        Assert.That(metrics.PropagationCost, Is.EqualTo(0.5).Within(1e-9));
    }

    [Test]
    public void NoDependencies_HasZeroPropagationCost()
    {
        Type("A");
        Type("B");
        Type("C");

        var metrics = SystemMetricsAnalysis.Calculate(_graph);

        Assert.That(metrics.PropagationCost, Is.EqualTo(0.0));
        Assert.That(metrics.TypeDependencyCount, Is.EqualTo(0));
    }

    [Test]
    public void FullyConnected_HasPropagationCostOne()
    {
        // Every type reaches every other type.
        var a = Type("A");
        var b = Type("B");
        var c = Type("C");
        Depend(a, b);
        Depend(b, c);
        Depend(c, a);

        var metrics = SystemMetricsAnalysis.Calculate(_graph);

        Assert.That(metrics.PropagationCost, Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void DuplicateAndSelfEdges_AreIgnored()
    {
        var a = Type("A");
        var b = Type("B");
        Depend(a, b);
        Depend(a, b); // duplicate: still one type dependency
        Depend(a, a); // self edge: dropped

        var metrics = SystemMetricsAnalysis.Calculate(_graph);

        Assert.That(metrics.TypeDependencyCount, Is.EqualTo(1));
        // A reaches B, B reaches nothing => 1 of 2 pairs.
        Assert.That(metrics.PropagationCost, Is.EqualTo(0.5).Within(1e-9));
    }

    [Test]
    public void FewerThanTwoTypes_ReturnsZero()
    {
        Type("Alone");

        var metrics = SystemMetricsAnalysis.Calculate(_graph);

        Assert.That(metrics.TypeCount, Is.EqualTo(1));
        Assert.That(metrics.PropagationCost, Is.EqualTo(0.0));
    }
}
