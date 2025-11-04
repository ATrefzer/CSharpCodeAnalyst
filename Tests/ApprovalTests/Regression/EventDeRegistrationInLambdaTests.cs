using Contracts.Graph;

namespace CodeParserTests.ApprovalTests.Regression;

[TestFixture]
public class EventDeRegistrationInLambdaTests : ApprovalTestBase
{
    private CodeGraph GetTestGraph()
    {
        return GetTestGraph("Regression.SpecificBugs.global.Regression.SpecificBugs.EventDeRegistrationInLambda");
    }

    [Test]
    public void Detects_deregistration_in_lambda()
    {
        var graph = GetTestGraph();

        var handler = graph.Nodes.Values.First(n => n.ElementType == CodeElementType.Method && n is { Name: "MyHandler", Parent.Name: "EventDeRegistrationInLambda" });
        var handles = graph.GetAllRelationships().Single(r => r.Type == RelationshipType.Handles && r.SourceId == handler.Id);

        Assert.IsTrue(handles.HasAttribute(RelationshipAttribute.EventRegistration));
        Assert.IsTrue(handles.HasAttribute(RelationshipAttribute.EventUnregistration));
    }

    [Test]
    public void Classes_should_be_detected()
    {
        var classes = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.EventDeRegistrationInLambda.EventDeRegistrationInLambda",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.EventDeRegistrationInLambda.Extensions",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.EventDeRegistrationInLambda.Source"
        };

        CollectionAssert.AreEquivalent(expected, classes.OrderBy(x => x).ToArray());
    }

    [Test]
    public void MethodCalls_should_be_detected()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        var expected = new string[]
        {
        };
        CollectionAssert.AreEquivalent(expected, calls);
    }
}