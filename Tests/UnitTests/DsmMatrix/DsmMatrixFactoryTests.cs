using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Features.DsmMatrix;

namespace CodeParserTests.UnitTests.DsmMatrix;

[TestFixture]
public class DsmMatrixFactoryTests
{
    private static TestCodeGraph BuildGraph()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("Asm");
        var ns = graph.CreateNamespace("Ns", assembly);
        var a = graph.CreateClass("A", ns);
        var b = graph.CreateClass("B", ns);
        a.Relationships.Add(new Relationship(a.Id, b.Id, RelationshipType.Uses));
        return graph;
    }

    [Test]
    public void Create_BuildsAMatrixOverTheWholeGraph()
    {
        var viewModel = DsmMatrixFactory.Create(BuildGraph());

        Assert.That(viewModel.ActiveMatrix, Is.Not.Null);
        Assert.That(viewModel.ActiveMatrix.MatrixSize, Is.GreaterThan(0));
    }

    [Test]
    public void Create_OnABackgroundThread_DoesNotNeedADispatcher()
    {
        // MainViewModel.OnShowDsm builds this inside Task.Run, because the work is quadratic in the number
        // of types. That only holds as long as nothing in DsmSuite's view models has thread affinity, so
        // pin it down: a plain worker thread with no Dispatcher of its own has to get through.
        var graph = BuildGraph();
        Exception? failure = null;
        object? result = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = DsmMatrixFactory.Create(graph);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.Start();
        var finished = thread.Join(TimeSpan.FromSeconds(30));

        Assert.That(finished, Is.True, "building the matrix blocked on a background thread");
        Assert.That(failure, Is.Null, $"building the matrix threw: {failure}");
        Assert.That(result, Is.Not.Null);
    }
}
