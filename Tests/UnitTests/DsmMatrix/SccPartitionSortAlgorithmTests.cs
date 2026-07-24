using System.Diagnostics;
using System.Reflection;
using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Features.DsmMatrix;
using DsmSuite.DsmViewer.Model.Core;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace CodeParserTests.UnitTests.DsmMatrix;

[TestFixture]
public class SccPartitionSortAlgorithmTests
{

    private static DsmModel CreateModel()
    {
        return new DsmModel("test", Assembly.GetExecutingAssembly());
    }

    private static IDsmElement AddChild(DsmModel model, string name)
    {
        return model.AddElement(name, "Class", model.RootElement.Id, null, null);
    }

    [Test]
    public void Chain_PutsConsumersOnTopAndProvidersAtTheBottom()
    {
        var model = CreateModel();

        // Insert in reverse order to prove the sort actually reorders: C, B, A with
        // A -> B -> C ("->" = consumes).
        var c = AddChild(model, "C");
        var b = AddChild(model, "B");
        var a = AddChild(model, "A");
        model.AddRelation(a, b, "Dependency", 1, null);
        model.AddRelation(b, c, "Dependency", 1, null);

        var result = new SccPartitionSortAlgorithm(model, model.RootElement).Sort();

        // order[newPosition] = originalIndex; expected top-to-bottom: A(2), B(1), C(0).
        Assert.That(result.GetOrder(), Is.EqualTo(new[] { 2, 1, 0 }));
    }

    [Test]
    public void Cycle_StaysTogetherAsBlockAboveItsProvider()
    {
        var model = CreateModel();

        var a = AddChild(model, "A");
        var b = AddChild(model, "B");
        var shared = AddChild(model, "Shared");
        model.AddRelation(a, b, "Dependency", 1, null);
        model.AddRelation(b, a, "Dependency", 1, null);
        model.AddRelation(a, shared, "Dependency", 1, null);
        model.AddRelation(b, shared, "Dependency", 1, null);

        var result = new SccPartitionSortAlgorithm(model, model.RootElement).Sort();

        // The cycle block {A, B} keeps its original order and the shared provider goes below.
        Assert.That(result.GetOrder(), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void IndependentSiblings_KeepTheirExistingOrder()
    {
        var model = CreateModel();
        AddChild(model, "X");
        AddChild(model, "Y");
        AddChild(model, "Z");

        var result = new SccPartitionSortAlgorithm(model, model.RootElement).Sort();

        Assert.That(result.GetOrder(), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    /// <summary>
    ///     The reason this algorithm exists: DsmSuite's brute-force partitioning took 13 s for 100
    ///     flat siblings and minutes for 200 (the "DSM conversion runs forever" report). The full
    ///     builder over a flat graph with hundreds of types has to finish in seconds.
    /// </summary>
    [Test]
    public void FlatImportedGraph_PartitionsQuickly()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("A");
        var ns = graph.CreateNamespace("ns", assembly);
        var classes = new List<CodeElement>();
        for (var i = 0; i < 300; i++)
        {
            classes.Add(graph.CreateClass($"C{i}", ns));
        }

        var random = new Random(42);
        foreach (var source in classes)
        {
            for (var k = 0; k < 10; k++)
            {
                var target = classes[random.Next(classes.Count)];
                if (target.Id != source.Id)
                {
                    source.Relationships.Add(new Relationship(source.Id, target.Id, RelationshipType.Uses));
                }
            }
        }

        var model = CreateModel();
        var stopwatch = Stopwatch.StartNew();
        var typeCount = new CodeGraphToDsmModelBuilder(model, graph).Build();
        stopwatch.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(typeCount, Is.EqualTo(300));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public void LargeChainAndCycles_ProduceAValidPermutation()
    {
        var model = CreateModel();
        var children = new List<IDsmElement>();
        for (var i = 0; i < 500; i++)
        {
            children.Add(AddChild(model, $"E{i}"));
        }

        // A long chain with a back edge every 50 elements (cycles of size 50).
        for (var i = 0; i < children.Count - 1; i++)
        {
            model.AddRelation(children[i], children[i + 1], "Dependency", 1, null);
            if (i % 50 == 49)
            {
                model.AddRelation(children[i], children[i - 49], "Dependency", 1, null);
            }
        }

        var result = new SccPartitionSortAlgorithm(model, model.RootElement).Sort();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.GetOrder().OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, children.Count)));
        });
    }
}
