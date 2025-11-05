using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests.Regression;

[TestFixture]
public class AssignmentDuplicateTests : ApprovalTestBase
{
    private CodeGraph.Graph.CodeGraph GetTestGraph()
    {
        return GetTestGraph("Regression.SpecificBugs.global.Regression.SpecificBugs.AssignmentDuplicate");
    }


    [Test]
    public void AssignmentExpressions_ShouldNotCreateDuplicateRelationships()
    {
        var graph = GetTestGraph();

        // Find our test method
        var testMethod = graph.Nodes.Values
            .FirstOrDefault(n => n.Name == "TestMethod" && n.FullName.Contains("AssignmentDuplicate"));

        Assert.That(testMethod != null);

        // Check relationships from TestMethod
        var relationships = testMethod.Relationships;

        // Count how many times TestProperty is referenced
        var testPropertyRelationships = relationships
            .Where(r => r.Type == RelationshipType.Calls)
            .Where(r =>
            {
                var target = graph.Nodes.GetValueOrDefault(r.TargetId);
                return target?.Name == "TestProperty";
            })
            .ToList();

        // Count how many times TestField is referenced
        var testFieldRelationships = relationships
            .Where(r => r.Type == RelationshipType.Uses)
            .Where(r =>
            {
                var target = graph.Nodes.GetValueOrDefault(r.TargetId);
                return target?.Name == "TestField";
            })
            .ToList();

        // Check for duplicate SourceLocations in the same relationship
        foreach (var rel in testPropertyRelationships.Concat(testFieldRelationships))
        {
            // Group by line number to check for duplicates in same line
            var sourceLocationsByLine = rel.SourceLocations.GroupBy(loc => loc.Line);

            foreach (var lineGroup in sourceLocationsByLine)
            {
                if (lineGroup.Count() > 1)
                {
                    Assert.Fail($"Found {lineGroup.Count()} SourceLocations for line {lineGroup.Key} in relationship to " +
                                $"{graph.Nodes.GetValueOrDefault(rel.TargetId)?.Name}. " +
                                $"Columns: {string.Join(", ", lineGroup.Select(loc => loc.Column))}");
                }
            }
        }

        // Also check that we have the expected number of relationships
        Assert.That(testPropertyRelationships.Count > 0);
        Assert.That(testFieldRelationships.Count > 0);
    }

    [Test]
    public void Classes_should_be_detected()
    {
        var classes = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.AssignmentDuplicate.AssignmentDuplicate"
        };

        Assert.That(classes.OrderBy(x => x).ToArray(), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_should_be_detected()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.AssignmentDuplicate.AssignmentDuplicate.TestMethod -> Regression.SpecificBugs.global.Regression.SpecificBugs.AssignmentDuplicate.AssignmentDuplicate.TestProperty"
        };

        Assert.That(calls, Is.EquivalentTo(expected));
    }
}