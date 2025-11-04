using Contracts.Graph;

namespace CodeParserTests.ApprovalTests.Regression;

[TestFixture]
public class AssignmentDuplicateTests : ApprovalTestBase
{
    private CodeGraph GetTestAssemblyGraph()
    {
        return GetTestGraph("Regression.SpecificBugs");
    }
    

    [Test]
    public void AssignmentExpressions_ShouldNotCreateDuplicateRelationships()
    {
        var graph = GetTestAssemblyGraph();

        // Find our test method
        var testMethod = graph.Nodes.Values
            .FirstOrDefault(n => n.Name == "TestMethod" && n.FullName.Contains("AssignmentDuplicate"));

        Assert.IsNotNull(testMethod, "TestMethod not found in AssignmentDuplicateTest");

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
        Assert.Greater(testPropertyRelationships.Count, 0, "Should have TestProperty relationships");
        Assert.Greater(testFieldRelationships.Count, 0, "Should have TestField relationships");
    }

    [Test]
    public void MemberAccessExpressions_ShouldNotCreateDuplicateRelationships()
    {
        var graph = GetTestAssemblyGraph();

        // Find our test method
        var testMethod = graph.Nodes.Values
            .FirstOrDefault(n => n.Name == "TestMethod" && n.FullName.Contains("MemberAccessDuplicate"));

        Assert.IsNotNull(testMethod, "TestMethod not found in MemberAccessDuplicateTest");

        // Check relationships from TestMethod
        var relationships = testMethod.Relationships;

        // Find OriginalElement property relationships
        var originalElementRelationships = relationships
            .Where(r => r.Type == RelationshipType.Calls)
            .Where(r =>
            {
                var target = graph.Nodes.GetValueOrDefault(r.TargetId);
                return target?.Name == "OriginalElement";
            })
            .ToList();

        Assert.Greater(originalElementRelationships.Count, 0, "Should have OriginalElement relationships");

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
}