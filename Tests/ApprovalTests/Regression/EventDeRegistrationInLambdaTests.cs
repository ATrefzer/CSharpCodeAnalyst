using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests.Regression;

[TestFixture]
public class EventDeRegistrationInLambdaTests : ApprovalTestBase
{
    private CodeGraph.Graph.CodeGraph GetTestGraph()
    {
        return GetTestGraph("Regression.SpecificBugs.global.Regression.SpecificBugs.EventDeRegistrationInLambda");
    }

    [Test]
    public void Detects_deregistration_in_lambda()
    {
        var graph = GetTestGraph();

        var handler = graph.Nodes.Values.First(n => n.ElementType == CodeElementType.Method && n is { Name: "MyHandler", Parent.Name: "EventDeRegistrationInLambda" });
        var handles = graph.GetAllRelationships().Single(r => r.Type == RelationshipType.Handles && r.SourceId == handler.Id);

        Assert.That(handles.HasAttribute(RelationshipAttribute.EventRegistration));
        Assert.That(handles.HasAttribute(RelationshipAttribute.EventUnregistration));
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

        Assert.That(classes.OrderBy(x => x).ToArray(), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_should_be_detected()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.EventDeRegistrationInLambda.EventDeRegistrationInLambda.Do -> Regression.SpecificBugs.global.Regression.SpecificBugs.EventDeRegistrationInLambda.Extensions.LoopOver"
        };
        Assert.That(calls, Is.EquivalentTo(expected));
    }
}