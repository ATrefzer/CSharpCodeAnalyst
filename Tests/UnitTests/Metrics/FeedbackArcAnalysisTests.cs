using CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

namespace CodeParserTests.UnitTests.Metrics;

[TestFixture]
public class FeedbackArcAnalysisTests
{
    private readonly Dictionary<string, List<string>> _edges = new();
    private readonly HashSet<string> _vertices = [];

    [SetUp]
    public void SetUp()
    {
        _vertices.Clear();
        _edges.Clear();
    }

    private void Vertex(params string[] names)
    {
        foreach (var name in names)
        {
            _vertices.Add(name);
            _edges.TryAdd(name, []);
        }
    }

    private void Edge(string from, string to)
    {
        Vertex(from, to);
        _edges[from].Add(to);
    }

    private FeedbackArcResult Analyze()
    {
        return FeedbackArcAnalysis.Analyze(TypeGraph.FromAdjacency(_vertices, _edges));
    }

    [Test]
    public void Chain_IsPerfectlyLayered_ZeroFeedback()
    {
        // A -> B -> C: a clean DAG, orders topologically, nothing points backward.
        Edge("A", "B");
        Edge("B", "C");

        var result = Analyze();

        Assert.That(result.EdgeCount, Is.EqualTo(2));
        Assert.That(result.FeedbackEdges, Is.Empty);
        Assert.That(result.FeedbackDensity, Is.EqualTo(0.0));
    }

    [Test]
    public void Tree_IsPerfectlyLayered_ZeroFeedback()
    {
        // A branching DAG still has no cycles, so no feedback edges.
        Edge("A", "B");
        Edge("A", "C");
        Edge("B", "D");
        Edge("C", "D");

        var result = Analyze();

        Assert.That(result.FeedbackDensity, Is.EqualTo(0.0));
    }

    [Test]
    public void TwoCycle_HalfTheEdgesAreFeedback()
    {
        // A <-> B: one of the two edges must point backward.
        Edge("A", "B");
        Edge("B", "A");

        var result = Analyze();

        Assert.That(result.EdgeCount, Is.EqualTo(2));
        Assert.That(result.FeedbackEdges.Count, Is.EqualTo(1));
        Assert.That(result.FeedbackDensity, Is.EqualTo(0.5).Within(1e-9));
    }

    [Test]
    public void ThreeCycle_OneEdgeIsFeedback()
    {
        // A -> B -> C -> A: exactly one edge must be cut to break the cycle.
        Edge("A", "B");
        Edge("B", "C");
        Edge("C", "A");

        var result = Analyze();

        Assert.That(result.EdgeCount, Is.EqualTo(3));
        Assert.That(result.FeedbackEdges.Count, Is.EqualTo(1));
        Assert.That(result.FeedbackDensity, Is.EqualTo(1.0 / 3.0).Within(1e-9));
    }

    [Test]
    public void CycleWithForwardChords_CountsOnlyTheBackEdge()
    {
        // A -> B -> C -> A plus forward chords A -> C: only C -> A is feedback (1 of 4).
        Edge("A", "B");
        Edge("B", "C");
        Edge("C", "A");
        Edge("A", "C");

        var result = Analyze();

        Assert.That(result.EdgeCount, Is.EqualTo(4));
        Assert.That(result.FeedbackEdges.Count, Is.EqualTo(1));
        Assert.That(result.FeedbackEdges.Single(), Is.EqualTo(("C", "A")));
        Assert.That(result.FeedbackDensity, Is.EqualTo(0.25).Within(1e-9));
    }

    [Test]
    public void DisjointCycleAndChain_OnlyTheCycleContributes()
    {
        // A <-> B (cycle, 2 edges) and C -> D -> E (chain, 2 edges). 1 feedback of 4 edges.
        Edge("A", "B");
        Edge("B", "A");
        Edge("C", "D");
        Edge("D", "E");

        var result = Analyze();

        Assert.That(result.EdgeCount, Is.EqualTo(4));
        Assert.That(result.FeedbackEdges.Count, Is.EqualTo(1));
        Assert.That(result.FeedbackDensity, Is.EqualTo(0.25).Within(1e-9));
    }

    [Test]
    public void CrossComponentEdges_AreNeverFeedback()
    {
        // Two independent cycles chained by a forward edge between them. Each cycle contributes one
        // back edge; the connecting edge X -> is forward. 2 feedback of 5 edges.
        Edge("A", "B");
        Edge("B", "A"); // cycle 1
        Edge("B", "C"); // bridge (acyclic between the two SCCs)
        Edge("C", "D");
        Edge("D", "C"); // cycle 2

        var result = Analyze();

        Assert.That(result.EdgeCount, Is.EqualTo(5));
        Assert.That(result.FeedbackEdges.Count, Is.EqualTo(2));
        Assert.That(result.FeedbackDensity, Is.EqualTo(2.0 / 5.0).Within(1e-9));
    }

    [Test]
    public void CompleteDigraph_IsMaximallyTangled()
    {
        // Every ordered pair present. Half of all edges are always backward regardless of order.
        var names = new[] { "A", "B", "C", "D" };
        foreach (var from in names)
        {
            foreach (var to in names)
            {
                if (from != to)
                {
                    Edge(from, to);
                }
            }
        }

        var result = Analyze();

        var n = names.Length;
        Assert.That(result.EdgeCount, Is.EqualTo(n * (n - 1)));
        // A tournament/complete digraph: exactly half the edges point backward for any linear order.
        Assert.That(result.FeedbackEdges.Count, Is.EqualTo(n * (n - 1) / 2));
        Assert.That(result.FeedbackDensity, Is.EqualTo(0.5).Within(1e-9));
    }

    [Test]
    public void NoEdges_IsZero()
    {
        Vertex("A", "B", "C");

        var result = Analyze();

        Assert.That(result.EdgeCount, Is.EqualTo(0));
        Assert.That(result.FeedbackDensity, Is.EqualTo(0.0));
    }

    [Test]
    public void OrderIsAPermutationOfAllVertices()
    {
        Edge("A", "B");
        Edge("B", "C");
        Edge("C", "A");
        Vertex("Lonely");

        var result = Analyze();

        Assert.That(result.Order, Is.EquivalentTo(new[] { "A", "B", "C", "Lonely" }));
    }
}
