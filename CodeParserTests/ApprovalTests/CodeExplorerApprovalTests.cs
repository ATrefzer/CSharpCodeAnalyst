using CSharpCodeAnalyst.Exploration;

namespace CodeParserTests.ApprovalTests;

public class CodeExplorerApprovalTests : ProjectTestBase
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

        var actualRelationships = result.Relationships.Select(d =>
                $"{Graph.Nodes[d.SourceId].FullName} -({d.Type})-> {Graph.Nodes[d.TargetId].FullName}")
            .OrderBy(x => x);

        var expectedRelationships = new List<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -(Overrides)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            /* ----- */
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            /* ----- */
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            /* ----- */"Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            /* ----- */ /* ----- */
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor -(Calls)-> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build"
        };
        CollectionAssert.AreEquivalent(expectedRelationships, actualRelationships);


        var actualElements = result.Elements.Select(m => m.FullName).ToList();

        var expectedElements = new List<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor"
        };
        CollectionAssert.AreEquivalent(expectedElements, actualElements);
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
        CollectionAssert.AreEquivalent(expectedRelationships, actualRelationships);


        var actualElements = result.Elements.Select(m => m.FullName).ToList();

        var expectedElements = new List<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor"
        };
        CollectionAssert.AreEquivalent(expectedElements, actualElements);
    }
}