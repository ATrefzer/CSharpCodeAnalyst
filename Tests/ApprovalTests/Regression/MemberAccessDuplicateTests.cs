using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests.Regression;

[TestFixture]
public class MemberAccessDuplicateTests : ApprovalTestBase
{
    private CodeGraph.Graph.CodeGraph GetTestGraph()
    {
        return GetTestGraph("Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicate");
    }

    [Test]
    public void MemberAccessExpressions_ShouldNotCreateDuplicateRelationships()
    {
        var graph = GetTestGraph();

        // Find our test method
        var testMethod = graph.Nodes.Values
            .FirstOrDefault(n => n.Name == "TestMethod" && n.FullName.Contains("MemberAccessDuplicate"));

        Assert.That(testMethod != null);

        // Check relationships from TestMethod
        var relationships = testMethod!.Relationships;

        // Find OriginalElement property relationships
        var originalElementRelationships = relationships
            .Where(r => r.Type == RelationshipType.Calls)
            .Where(r =>
            {
                var target = graph.Nodes.GetValueOrDefault(r.TargetId);
                return target?.Name == "OriginalElement";
            })
            .ToList();

        Assert.That(originalElementRelationships.Count > 0);

        // Check for duplicate SourceLocations in the same relationship
        foreach (var rel in originalElementRelationships)
        {
            // Group by line number to check for duplicates in same line
            var sourceLocationsByLine = rel.SourceLocations.GroupBy(loc => loc.Line);

            foreach (var lineGroup in sourceLocationsByLine)
            {
                if (lineGroup.Count() > 1)
                {
                    Assert.Fail($"Found {lineGroup.Count()} SourceLocations for line {lineGroup.Key} in relationship to OriginalElement. " +
                                $"Columns: {string.Join(", ", lineGroup.Select(loc => loc.Column))}. " +
                                $"This indicates the same property access is being processed twice by different analyzers.");
                }
            }
        }
    }

    [Test]
    public void Classes_should_be_detected()
    {
        var classes = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicate.MemberAccessDuplicate",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicate.SearchGraphSource",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicate.SearchNode"
        };

        Assert.That(classes.OrderBy(x => x).ToArray(), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_should_be_detected()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicate.MemberAccessDuplicate.TestMethod -> Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicate.SearchGraphSource.OriginalElement",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicate.MemberAccessDuplicate.TestMethod -> Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicate.SearchNode.Name"
        };
        Assert.That(calls, Is.EquivalentTo(expected));
    }
}