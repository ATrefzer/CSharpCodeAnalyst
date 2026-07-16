using CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

namespace CodeParserTests.UnitTests.Metrics;

/// <summary>
///     Calibration of the MDL tangledness against the values computed by hand on paper.
///     The running example: 6 types, 2 modules of 3, always the same 8 edges - only their
///     placement relative to the module boundaries changes.
///     Slots: A-&gt;A = 3*2 = 6, B-&gt;B = 3*2 = 6, A-&gt;B = 3*3 = 9, B-&gt;A = 3*3 = 9, total 30 = 6*5.
///     Baseline: 30 * H(8/30 = 0.267) = 30 * 0.837 = 25.10 bits (identical in both arrangements).
/// </summary>
[TestFixture]
public class ModularityAnalysisTests
{
    private readonly Dictionary<string, List<string>> _edges = new();
    private readonly Dictionary<string, string> _modules = new();
    private readonly HashSet<string> _vertices = [];

    [SetUp]
    public void SetUp()
    {
        _vertices.Clear();
        _edges.Clear();
        _modules.Clear();
    }

    private void Type(string name, string module)
    {
        _vertices.Add(name);
        _edges.TryAdd(name, []);
        _modules[name] = module;
    }

    private void Edge(string from, string to)
    {
        _edges[from].Add(to);
    }

    private double Tangledness()
    {
        var value = ModularityAnalysis.CalculateTangledness(TypeGraph.FromAdjacency(_vertices, _edges), _modules);
        TestContext.Out.WriteLine($"tangledness = {value:F4}");
        return value;
    }

    /// <summary>The two modules of the running example, three types each.</summary>
    private void TwoModulesOfThree()
    {
        Type("a1", "A");
        Type("a2", "A");
        Type("a3", "A");
        Type("b1", "B");
        Type("b2", "B");
        Type("b3", "B");
    }

    [Test]
    public void CleanModules_DependenciesRespectTheBoundaries_IsLow()
    {
        TwoModulesOfThree();

        // A -> A: all 6 slots filled. B -> B: 2 of 6. Nothing crosses.
        Edge("a1", "a2");
        Edge("a1", "a3");
        Edge("a2", "a1");
        Edge("a2", "a3");
        Edge("a3", "a1");
        Edge("a3", "a2");
        Edge("b1", "b2");
        Edge("b2", "b3");

        // L_block = 6*H(1) + 6*H(1/3) + 9*H(0) + 9*H(0)
        //         = 0     + 5.51     + 0      + 0       = 5.51 bits
        // tangledness = 5.51 / 25.10 = 0.2195
        Assert.That(Tangledness(), Is.EqualTo(0.2195).Within(0.001));
    }

    [Test]
    public void TangledModules_SameEdgesSpreadAcross_IsHigh()
    {
        TwoModulesOfThree();

        // The same 8 edges, but scattered so every block looks like the global average.
        Edge("a1", "a2"); // A -> A : 2 of 6
        Edge("a2", "a3");
        Edge("b1", "b2"); // B -> B : 2 of 6
        Edge("b2", "b3");
        Edge("a1", "b1"); // A -> B : 2 of 9
        Edge("a3", "b2");
        Edge("b1", "a1"); // B -> A : 2 of 9
        Edge("b3", "a3");

        // L_block = 6*H(1/3) + 6*H(1/3) + 9*H(2/9) + 9*H(2/9)
        //         = 5.51     + 5.51     + 6.88     + 6.88     = 24.78 bits
        // tangledness = 24.78 / 25.10 = 0.9871
        Assert.That(Tangledness(), Is.EqualTo(0.9871).Within(0.001));
    }

    [Test]
    public void SingleModuleHoldingEverything_ExplainsNothing_IsExactlyOne()
    {
        // One module means the only block IS the whole matrix, with exactly the global density.
        // The block model degenerates into the baseline, so no bits are saved at all.
        Type("a1", "M");
        Type("a2", "M");
        Type("a3", "M");
        Edge("a1", "a2");
        Edge("a2", "a3");

        Assert.That(Tangledness(), Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void OneModulePerType_IsZero_TheGranularityArtifact()
    {
        // Degenerate opposite end: every type in its own module. Internal blocks have 1*0 = 0 slots,
        // every cross block holds exactly one slot that is either empty or full - both free to
        // describe. The result is a "perfect" 0 without any insight, which is why the value may only
        // be compared between runs using the same partition granularity.
        Type("a1", "M1");
        Type("a2", "M2");
        Type("a3", "M3");
        Edge("a1", "a2");
        Edge("a2", "a3");

        Assert.That(Tangledness(), Is.EqualTo(0.0));
    }

    [Test]
    public void NoEdges_IsZero()
    {
        Type("a1", "A");
        Type("b1", "B");

        Assert.That(Tangledness(), Is.EqualTo(0.0));
    }

    [Test]
    public void UnequalModuleSizes_AreWeightedBySlots_NotByModuleCount()
    {
        // Tiny module T (2 types, perfectly coherent) next to a bigger, messy module M (4 types).
        // Slots: T->T = 2, M->M = 12, T->M = 8, M->T = 8  => 30 total.
        Type("t1", "T");
        Type("t2", "T");
        Type("m1", "M");
        Type("m2", "M");
        Type("m3", "M");
        Type("m4", "M");

        Edge("t1", "t2"); // T -> T : 2 of 2  -> H(1) = 0, the tiny module is free ...
        Edge("t2", "t1");

        Edge("m1", "m2"); // M -> M : 6 of 12 -> H(0.5) = 1 bit, maximally expensive ...
        Edge("m2", "m3");
        Edge("m3", "m4");
        Edge("m4", "m1");
        Edge("m1", "m3");
        Edge("m2", "m4");

        // L_block = 2*H(1) + 12*H(0.5) + 8*H(0) + 8*H(0) = 0 + 12 + 0 + 0 = 12 bits
        // tangledness = 12 / 25.10 = 0.478
        // ... and the flawless tiny module barely moves the result: the big block dominates.
        Assert.That(Tangledness(), Is.EqualTo(0.478).Within(0.001));
    }
}
