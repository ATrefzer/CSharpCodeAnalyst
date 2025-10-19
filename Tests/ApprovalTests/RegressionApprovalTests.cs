using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class RegressionApprovalTests : ApprovalTestBase
{
    private CodeGraph GetTestAssemblyGraph()
    {
        return GetAssemblyGraph("Regression.SpecificBugs");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.Base", "Regression.SpecificBugs.global.Regression.SpecificBugs.Driver", "Regression.SpecificBugs.global.Regression.SpecificBugs.ExtendedType",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.Extensions", "Regression.SpecificBugs.global.Regression.SpecificBugs.PartialClient",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.ViewModelAdapter1", "Regression.SpecificBugs.global.Regression.SpecificBugs.ViewModelAdapter2",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.AssignmentDuplicateTest",

            "Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicateTest",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.SearchGraphSource",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.SearchNode"
        };

        CollectionAssert.AreEquivalent(expected, classes.OrderBy(x => x).ToArray());
    }

    [Test]
    public void Records_ShouldBeDetected()
    {
        var records = GetAllNodesOfType(GetTestAssemblyGraph(), CodeElementType.Record);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordA",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordB"
        };

        CollectionAssert.AreEquivalent(expected, records);
    }

    [Test]
    public void Structs_ShouldBeDetected()
    {
        var structs = GetAllNodesOfType(GetTestAssemblyGraph(), CodeElementType.Struct);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.StructWithInterface"
        };

        CollectionAssert.AreEquivalent(expected, structs);
    }

    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var calls = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.Base.AddToSlave -> Regression.SpecificBugs.global.Regression.SpecificBugs.Base.AddToSlave",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.Base.Build -> Regression.SpecificBugs.global.Regression.SpecificBugs.Base.AddToSlave",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.ViewModelAdapter1.AddToSlave -> Regression.SpecificBugs.global.Regression.SpecificBugs.Base.AddToSlave",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.ViewModelAdapter2.AddToSlave -> Regression.SpecificBugs.global.Regression.SpecificBugs.Base.AddToSlave",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.Driver..ctor -> Regression.SpecificBugs.global.Regression.SpecificBugs.Base.Build",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.StructWithInterface.CompareTo -> Regression.SpecificBugs.global.Regression.SpecificBugs.StructWithInterface.Value",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.Extensions.Slice -> Regression.SpecificBugs.global.Regression.SpecificBugs.ExtendedType.Data",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.PartialClient.CreateInstance -> Regression.SpecificBugs.global.Regression.SpecificBugs.PartialClient.OnCreated",

            "Regression.SpecificBugs.global.Regression.SpecificBugs.AssignmentDuplicateTest.TestMethod -> Regression.SpecificBugs.global.Regression.SpecificBugs.AssignmentDuplicateTest.TestProperty",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicateTest.TestMethod -> Regression.SpecificBugs.global.Regression.SpecificBugs.SearchGraphSource.OriginalElement",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.MemberAccessDuplicateTest.TestMethod -> Regression.SpecificBugs.global.Regression.SpecificBugs.SearchNode.Name"
        };
        CollectionAssert.AreEquivalent(expected, calls);
    }

    private IEnumerable<Relationship> GetVirtualMethodCallsInProject(string projectFilter)
    {
        return Graph.GetAllRelationships()
            .Where(r => r.Type == RelationshipType.Calls)
            .Where(r => IsInProject(r, projectFilter));
    }

    private IEnumerable<string> GetExtensionMethodsInProject(string projectName)
    {
        return Graph.Nodes.Values
            .Where(e => e.ElementType == CodeElementType.Method && e.FullName.Contains(projectName))
            .Where(e => e.FullName.Contains("Extensions"))
            .Select(e => $"{e.FullName.Split('.').Skip(e.FullName.Split('.').Length - 2).First()}.{e.Name}");
    }

    [Test]
    public void AssignmentExpressions_ShouldNotCreateDuplicateRelationships()
    {
        var graph = GetTestAssemblyGraph();

        // Find our test method
        var testMethod = graph.Nodes.Values
            .FirstOrDefault(n => n.Name == "TestMethod" && n.FullName.Contains("AssignmentDuplicateTest"));

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
            .FirstOrDefault(n => n.Name == "TestMethod" && n.FullName.Contains("MemberAccessDuplicateTest"));

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