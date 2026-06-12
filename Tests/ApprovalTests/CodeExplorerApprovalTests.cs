using CodeGraph.Exploration;

namespace CodeParserTests.ApprovalTests;

public class CodeExplorerApprovalTests : ApprovalTestBase
{
    [Test]
    public void CodeExplorer_FollowIncomingCalls_1()
    {
        // Scenario where base class calls base method of another instance.
        var codeElements = Graph.Nodes.Values;

        var explorer = new CodeGraphExplorer();
        explorer.LoadCodeGraph(Graph);

        var origin = codeElements.First(e =>
            e.FullName.Contains("Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave"));
        var result = explorer.FollowIncomingCallsHeuristically(origin.Id);

        var actual = result.Relationships.Select(d =>
                $"{Graph.Nodes[d.SourceId].FullName} -({d.Type})-> {Graph.Nodes[d.TargetId].FullName}")
            .OrderBy(x => x);

        var expected = new List<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -(Overrides)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            /* ----- */
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            /* ----- */
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            /* ----- */
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            /* ----- */ /* ----- */
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build"
        };
        Assert.That(actual, Is.EquivalentTo(expected));


        var actualElements = result.Elements.Select(m => m.FullName).ToList();

        var expectedElements = new List<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor"
        };
        Assert.That(actualElements, Is.EquivalentTo(expectedElements));
    }

    [Test]
    public void CodeExplorer_FollowIncomingCalls_2()
    {
        var codeElements = Graph.Nodes.Values;

        var explorer = new CodeGraphExplorer();
        explorer.LoadCodeGraph(Graph);

        var origin = codeElements.First(e =>
            e.FullName.Contains("Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave"));
        var result = explorer.FollowIncomingCallsHeuristically(origin.Id);

        var actualRelationships = result.Relationships.Select(d =>
            $"{Graph.Nodes[d.SourceId].FullName} -({d.Type})-> {Graph.Nodes[d.TargetId].FullName}");
        var expectedRelationships = new List<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -(Overrides)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build"
        };

        Assert.That(actualRelationships, Is.EquivalentTo(expectedRelationships));


        var actualElements = result.Elements.Select(m => m.FullName).ToList();

        var expectedElements = new List<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor"
        };
        Assert.That(actualElements, Is.EquivalentTo(expectedElements));
    }

    [Test]
    public void CodeExplorer_FollowIncomingCalls_ThisCallFromSiblingIsExcluded()
    {
        // this.Process() inside the sibling class Right resolves to Base.Process but can
        // never dispatch to Left.Process. It must be filtered like the implicit call.
        var result = FollowIncomingCalls("ThisCallSibling.Left.Process");

        var expectedRelationships = new List<string>
        {
            "FollowHeuristic.global.FollowHeuristic.ThisCallSibling.Left.Process -(Overrides)-> FollowHeuristic.global.FollowHeuristic.ThisCallSibling.Base.Process",
            "FollowHeuristic.global.FollowHeuristic.ThisCallSibling.Consumer.Use -(Calls)-> FollowHeuristic.global.FollowHeuristic.ThisCallSibling.Base.Process"
        };
        Assert.That(FormatRelationships(result), Is.EquivalentTo(expectedRelationships));

        var expectedElements = new List<string>
        {
            "FollowHeuristic.global.FollowHeuristic.ThisCallSibling.Left.Process",
            "FollowHeuristic.global.FollowHeuristic.ThisCallSibling.Base.Process",
            "FollowHeuristic.global.FollowHeuristic.ThisCallSibling.Consumer.Use"
        };
        Assert.That(result.Elements.Select(m => m.FullName).ToList(), Is.EquivalentTo(expectedElements));
    }

    [Test]
    public void CodeExplorer_FollowIncomingCalls_HierarchyRestrictionSurvivesStaticCall()
    {
        // Logger.Log is reached via a static call from the virtual method WorkerA.Work.
        // The implicit call to Work() inside the sibling WorkerB resolves to WorkerBase.Work
        // but can never dispatch to WorkerA.Work, so WorkerB.Other must be excluded.
        var result = FollowIncomingCalls("StaticReset.Logger.Log");

        var expectedRelationships = new List<string>
        {
            "FollowHeuristic.global.FollowHeuristic.StaticReset.WorkerA.Work -(Calls)-> FollowHeuristic.global.FollowHeuristic.StaticReset.Logger.Log",
            "FollowHeuristic.global.FollowHeuristic.StaticReset.WorkerA.Work -(Overrides)-> FollowHeuristic.global.FollowHeuristic.StaticReset.WorkerBase.Work",
            "FollowHeuristic.global.FollowHeuristic.StaticReset.WorkerBase.Drive -(Calls)-> FollowHeuristic.global.FollowHeuristic.StaticReset.WorkerBase.Work"
        };
        Assert.That(FormatRelationships(result), Is.EquivalentTo(expectedRelationships));

        var expectedElements = new List<string>
        {
            "FollowHeuristic.global.FollowHeuristic.StaticReset.Logger.Log",
            "FollowHeuristic.global.FollowHeuristic.StaticReset.WorkerA.Work",
            "FollowHeuristic.global.FollowHeuristic.StaticReset.WorkerBase.Work",
            "FollowHeuristic.global.FollowHeuristic.StaticReset.WorkerBase.Drive"
        };
        Assert.That(result.Elements.Select(m => m.FullName).ToList(), Is.EquivalentTo(expectedElements));
    }

    [Test]
    public void CodeExplorer_FollowIncomingCalls_ChainContinuesThroughProperty()
    {
        // The incoming call chain passes through the property getter Facade.Value.
        // The traversal must continue at the property and find Client.Consume as origin.
        var result = FollowIncomingCalls("PropertyChain.Repository.Compute");

        var expectedRelationships = new List<string>
        {
            "FollowHeuristic.global.FollowHeuristic.PropertyChain.Facade.Value -(Calls)-> FollowHeuristic.global.FollowHeuristic.PropertyChain.Repository.Compute",
            "FollowHeuristic.global.FollowHeuristic.PropertyChain.Client.Consume -(Calls)-> FollowHeuristic.global.FollowHeuristic.PropertyChain.Facade.Value"
        };
        Assert.That(FormatRelationships(result), Is.EquivalentTo(expectedRelationships));

        var expectedElements = new List<string>
        {
            "FollowHeuristic.global.FollowHeuristic.PropertyChain.Repository.Compute",
            "FollowHeuristic.global.FollowHeuristic.PropertyChain.Facade.Value",
            "FollowHeuristic.global.FollowHeuristic.PropertyChain.Client.Consume"
        };
        Assert.That(result.Elements.Select(m => m.FullName).ToList(), Is.EquivalentTo(expectedElements));
    }

    private SearchResult FollowIncomingCalls(string originFullNamePart)
    {
        var explorer = new CodeGraphExplorer();
        explorer.LoadCodeGraph(Graph);

        var origin = Graph.Nodes.Values.First(e => e.FullName.Contains(originFullNamePart));
        return explorer.FollowIncomingCallsHeuristically(origin.Id);
    }

    private List<string> FormatRelationships(SearchResult result)
    {
        return result.Relationships.Select(d =>
                $"{Graph.Nodes[d.SourceId].FullName} -({d.Type})-> {Graph.Nodes[d.TargetId].FullName}")
            .ToList();
    }
}