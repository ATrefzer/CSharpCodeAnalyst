using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests.Regression;

[TestFixture]
public class RecordsAndStructsTest : ApprovalTestBase
{
    private CodeGraph.Graph.CodeGraph GetTestGraph()
    {
        return GetTestGraph("Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs");
    }

    [Test]
    public void Classes_should_be_detected()
    {
        var classes = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.ExtendedType",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.Extensions",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.PartialClient"
        };

        CollectionAssert.AreEquivalent(expected, classes.OrderBy(x => x).ToArray());
    }

    [Test]
    public void Records_should_be_detected()
    {
        var records = GetAllNodesOfType(GetTestGraph(), CodeElementType.Record);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.RecordA",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.RecordB"
        };

        CollectionAssert.AreEquivalent(expected, records);
    }

    [Test]
    public void Structs_should_be_detected()
    {
        var structs = GetAllNodesOfType(GetTestGraph(), CodeElementType.Struct);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.StructWithInterface"
        };

        CollectionAssert.AreEquivalent(expected, structs);
    }

    [Test]
    public void MethodCalls_should_be_detected()
    {
        var calls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.StructWithInterface.CompareTo -> Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.StructWithInterface.Value",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.Extensions.Slice -> Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.ExtendedType.Data",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.PartialClient.CreateInstance -> Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.PartialClient.OnCreated"
        };
        CollectionAssert.AreEquivalent(expected, calls);
    }
}