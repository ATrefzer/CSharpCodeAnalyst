using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests.Regression;

[TestFixture]
public class FollowingIncomingCallsTests : ApprovalTestBase
{
    private CodeGraph.Graph.CodeGraph GetTestGraph()
    {
        return GetTestGraph("Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls");
    }

    [Test]
    public void Classes_should_be_detected()
    {
        var actual = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Base",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Driver",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.ViewModelAdapter1",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.ViewModelAdapter2"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_should_be_detected()
    {
        var actual = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Base.AddToSlave -> Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Base.AddToSlave",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Base.Build -> Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Base.AddToSlave",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.ViewModelAdapter1.AddToSlave -> Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Base.AddToSlave",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.ViewModelAdapter2.AddToSlave -> Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Base.AddToSlave",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Driver..ctor -> Regression.SpecificBugs.global.Regression.SpecificBugs.FollowIncomingCalls.Base.Build"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }
}