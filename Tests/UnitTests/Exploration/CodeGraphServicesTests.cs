using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Exploration;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Exploration;

/// <summary>
///     Focus on incoming / outgoing (deep) works on a canvas graph whose hierarchy is
///     intentionally incomplete: elements integrated via IntegrateCodeElementFromOriginal
///     get a parent link only while the parent itself is on the canvas. Containment is
///     therefore decided on the original element (full hierarchy), and afterwards the
///     surviving members are linked back into the focused container.
/// </summary>
[TestFixture]
public class CodeGraphServicesTests
{

    [SetUp]
    public void SetUp()
    {
        _master = new TestCodeGraph();

        _appAssembly = _master.CreateAssembly("App");
        _appNs = _master.CreateNamespace("App.Ns", _appAssembly);
        _appCls = _master.CreateClass("AppCls", _appNs);
        _appM = _master.CreateMethod("AppM", _appCls);

        _sdkAssembly = _master.CreateAssembly("Sdk");
        _sdkNs = _master.CreateNamespace("Sdk.Ns", _sdkAssembly);
        _sdkCls = _master.CreateClass("SdkCls", _sdkNs);
        _sdkM = _master.CreateMethod("SdkM", _sdkCls);
    }

    private TestCodeGraph _master = null!;
    private CodeElement _appAssembly = null!;
    private CodeElement _appNs = null!;
    private CodeElement _appCls = null!;
    private CodeElement _appM = null!;
    private CodeElement _sdkAssembly = null!;
    private CodeElement _sdkNs = null!;
    private CodeElement _sdkCls = null!;
    private CodeElement _sdkM = null!;

    private static CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph BuildCanvas(params CodeElement[] originalElements)
    {
        var canvas = new CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph();
        foreach (var element in originalElements)
        {
            canvas.IntegrateCodeElementFromOriginal(element);
        }

        return canvas;
    }

    private void AddCallOnCanvas(CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph canvas)
    {
        canvas.Nodes[_appM.Id].Relationships.Add(new Relationship(_appM.Id, _sdkM.Id, RelationshipType.Calls));
    }

    [Test]
    public void FocusOnOutgoingEdges_IncompleteCanvasHierarchy_KeepsCrossingEdge()
    {
        // The tutorial scenario: the SDK side is fully chained, the app side came in via
        // "incoming relationships (deep)" - method linked to its class, but the class is
        // free-standing because its namespace never made it to the canvas.
        var canvas = BuildCanvas(_sdkAssembly, _sdkNs, _sdkCls, _sdkM, _appAssembly, _appCls, _appM);
        AddCallOnCanvas(canvas);

        var result = CodeGraphServices.FocusOnOutgoingEdges(canvas, _appAssembly);

        Assert.That(result.Success, Is.True);
        var newGraph = result.NewGraph!;
        Assert.Multiple(() =>
        {
            // The crossing edge and both endpoints survive even though the app hierarchy
            // was incomplete on the canvas.
            Assert.That(newGraph.GetAllRelationships().Select(r => (r.SourceId, r.TargetId)),
                Is.EquivalentTo([(_appM.Id, _sdkM.Id)]));
            Assert.That(result.RemovedIds, Is.Empty);

            // The missing namespace was pulled in and the chain up to the focused
            // assembly is complete.
            Assert.That(result.AddedIds, Is.EquivalentTo([_appNs.Id]));
            Assert.That(newGraph.Nodes[_appM.Id].Parent?.Id, Is.EqualTo(_appCls.Id));
            Assert.That(newGraph.Nodes[_appCls.Id].Parent?.Id, Is.EqualTo(_appNs.Id));
            Assert.That(newGraph.Nodes[_appNs.Id].Parent?.Id, Is.EqualTo(_appAssembly.Id));
        });
    }

    [Test]
    public void FocusOnOutgoingEdges_FreeStandingEndpoints_CompletesOnlyTheFocusedContainer()
    {
        // Both endpoints free-standing; previously the focus removed everything but the
        // clicked assembly because its canvas clone had no children.
        var canvas = BuildCanvas(_appAssembly, _appM, _sdkM);
        AddCallOnCanvas(canvas);

        var result = CodeGraphServices.FocusOnOutgoingEdges(canvas, _appAssembly);

        var newGraph = result.NewGraph!;
        Assert.Multiple(() =>
        {
            Assert.That(newGraph.GetAllRelationships().Select(r => (r.SourceId, r.TargetId)),
                Is.EquivalentTo([(_appM.Id, _sdkM.Id)]));

            // The chain to the focused container is completed with clones of the missing
            // intermediate containers.
            Assert.That(result.AddedIds, Is.EquivalentTo([_appCls.Id, _appNs.Id]));
            Assert.That(newGraph.Nodes[_appM.Id].Parent?.Id, Is.EqualTo(_appCls.Id));
            Assert.That(newGraph.Nodes[_appCls.Id].Parent?.Id, Is.EqualTo(_appNs.Id));
            Assert.That(newGraph.Nodes[_appNs.Id].Parent?.Id, Is.EqualTo(_appAssembly.Id));

            // The endpoint outside the container stays as it is.
            Assert.That(newGraph.Nodes[_sdkM.Id].Parent, Is.Null);
        });
    }

    [Test]
    public void FocusOnIncomingEdges_FreeStandingEndpoints_CompletesOnlyTheFocusedContainer()
    {
        var canvas = BuildCanvas(_sdkAssembly, _appM, _sdkM);
        AddCallOnCanvas(canvas);

        var result = CodeGraphServices.FocusOnIncomingEdges(canvas, _sdkAssembly);

        var newGraph = result.NewGraph!;
        Assert.Multiple(() =>
        {
            Assert.That(newGraph.GetAllRelationships().Select(r => (r.SourceId, r.TargetId)),
                Is.EquivalentTo([(_appM.Id, _sdkM.Id)]));

            Assert.That(result.AddedIds, Is.EquivalentTo([_sdkCls.Id, _sdkNs.Id]));
            Assert.That(newGraph.Nodes[_sdkM.Id].Parent?.Id, Is.EqualTo(_sdkCls.Id));
            Assert.That(newGraph.Nodes[_sdkCls.Id].Parent?.Id, Is.EqualTo(_sdkNs.Id));
            Assert.That(newGraph.Nodes[_sdkNs.Id].Parent?.Id, Is.EqualTo(_sdkAssembly.Id));

            Assert.That(newGraph.Nodes[_appM.Id].Parent, Is.Null);
        });
    }

    [Test]
    public void FocusOnOutgoingEdges_NoCrossingEdges_KeepsOnlyTheContainer()
    {
        // An internal call does not cross the boundary; everything but the clicked
        // container is removed (existing semantics).
        var canvas = BuildCanvas(_appAssembly, _appCls, _appM);
        canvas.Nodes[_appM.Id].Relationships.Add(new Relationship(_appM.Id, _appCls.Id, RelationshipType.Calls));

        var result = CodeGraphServices.FocusOnOutgoingEdges(canvas, _appAssembly);

        var newGraph = result.NewGraph!;
        Assert.Multiple(() =>
        {
            Assert.That(newGraph.Nodes.Keys, Is.EquivalentTo([_appAssembly.Id]));
            Assert.That(result.RemovedIds, Is.EquivalentTo([_appCls.Id, _appM.Id]));
            Assert.That(result.AddedIds, Is.Empty);
            Assert.That(newGraph.GetAllRelationships(), Is.Empty);
        });
    }
}
