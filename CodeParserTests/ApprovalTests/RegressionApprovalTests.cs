using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class RegressionApprovalTests : ProjectTestBase
{
    private CodeGraph GetTestGraph()
    {
        return GetGraph("Regression.SpecificBugs");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Regression.SpecificBugs.Regression.SpecificBugs.Base", "Regression.SpecificBugs.Regression.SpecificBugs.Driver", "Regression.SpecificBugs.Regression.SpecificBugs.ExtendedType",
            "Regression.SpecificBugs.Regression.SpecificBugs.Extensions", "Regression.SpecificBugs.Regression.SpecificBugs.PartialClient",
            "Regression.SpecificBugs.Regression.SpecificBugs.ViewModelAdapter1", "Regression.SpecificBugs.Regression.SpecificBugs.ViewModelAdapter2"
        };

        CollectionAssert.AreEquivalent(expected, classes.OrderBy(x => x).ToArray());
    }

    [Test]
    public void Records_ShouldBeDetected()
    {
        var records = GetAllNodesOfType(GetTestGraph(), CodeElementType.Record);

        var expected = new[]
        {
            "Regression.SpecificBugs.Regression.SpecificBugs.RecordA", "Regression.SpecificBugs.Regression.SpecificBugs.RecordB"
        };

        CollectionAssert.AreEquivalent(expected, records);
    }

    [Test]
    public void Structs_ShouldBeDetected()
    {
        var structs = GetAllNodesOfType(GetTestGraph(), CodeElementType.Struct);

        var expected = new[]
        {
            "Regression.SpecificBugs.Regression.SpecificBugs.StructWithInterface"
        };

        CollectionAssert.AreEquivalent(expected, structs);
    }

    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        var dmp = DumpRelationships(calls);
        var expected = new[]
        {
            "Regression.SpecificBugs.Regression.SpecificBugs.Base.AddToSlave -> Regression.SpecificBugs.Regression.SpecificBugs.Base.AddToSlave",
            "Regression.SpecificBugs.Regression.SpecificBugs.Base.Build -> Regression.SpecificBugs.Regression.SpecificBugs.Base.AddToSlave",
            "Regression.SpecificBugs.Regression.SpecificBugs.ViewModelAdapter1.AddToSlave -> Regression.SpecificBugs.Regression.SpecificBugs.Base.AddToSlave",
            "Regression.SpecificBugs.Regression.SpecificBugs.ViewModelAdapter2.AddToSlave -> Regression.SpecificBugs.Regression.SpecificBugs.Base.AddToSlave",
            "Regression.SpecificBugs.Regression.SpecificBugs.Driver..ctor -> Regression.SpecificBugs.Regression.SpecificBugs.Base.Build",
            "Regression.SpecificBugs.Regression.SpecificBugs.StructWithInterface.CompareTo -> Regression.SpecificBugs.Regression.SpecificBugs.StructWithInterface.Value",
            "Regression.SpecificBugs.Regression.SpecificBugs.Extensions.Slice -> Regression.SpecificBugs.Regression.SpecificBugs.ExtendedType.Data",
            "Regression.SpecificBugs.Regression.SpecificBugs.PartialClient.CreateInstance -> Regression.SpecificBugs.Regression.SpecificBugs.PartialClient.OnCreated"
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
}