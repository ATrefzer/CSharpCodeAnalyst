using Contracts.Graph;

namespace CodeParserTests.ApprovalTests.Regression;

[TestFixture]
public class EventDeRegistrationInLambdaTests : ApprovalTestBase
{
    private CodeGraph GetTestAssemblyGraph()
    {
        return GetTestGraph("Regression.SpecificBugs");
    }

    [Test]
    public void Detects_deregistration_in_lambda()
    {
        var graph = GetTestAssemblyGraph();

        var handler = graph.Nodes.Values.First(n => n.ElementType == CodeElementType.Method && n is { Name: "MyHandler", Parent.Name: "EventDeRegistrationInLambda" });
        var handles = graph.GetAllRelationships().Single(r => r.Type == RelationshipType.Handles && r.SourceId == handler.Id);

        Assert.IsTrue(handles.HasAttribute(RelationshipAttribute.EventRegistration));
        Assert.IsTrue(handles.HasAttribute(RelationshipAttribute.EventUnregistration));
    }
}