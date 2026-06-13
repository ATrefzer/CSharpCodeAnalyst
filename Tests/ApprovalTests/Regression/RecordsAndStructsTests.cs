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
        var actual = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.ExtendedType",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.Extensions",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.PartialClient",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.Warehouse",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.Inventory"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void PrimaryConstructorParameterTypes_should_be_detected()
    {
        const string ns = "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs";
        var uses = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);

        // Positional record parameter types and class primary-constructor parameter types are
        // recorded as Uses on the type element.
        Assert.That(uses, Does.Contain($"{ns}.RecordA -> {ns}.RecordB"));
        Assert.That(uses, Does.Contain($"{ns}.RecordB -> {ns}.RecordA"));
        Assert.That(uses, Does.Contain($"{ns}.Inventory -> {ns}.Warehouse"));

        // The synthesized IEquatable<Self> of a record must not create a self-reference.
        Assert.That(uses, Does.Not.Contain($"{ns}.RecordA -> {ns}.RecordA"));
        Assert.That(uses, Does.Not.Contain($"{ns}.RecordB -> {ns}.RecordB"));
    }

    [Test]
    public void Records_should_be_detected()
    {
        var actual = GetAllNodesOfType(GetTestGraph(), CodeElementType.Record);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.RecordA",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.RecordB"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void Structs_should_be_detected()
    {
        var actual = GetAllNodesOfType(GetTestGraph(), CodeElementType.Struct);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.StructWithInterface"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_should_be_detected()
    {
        var actual = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);

        var expected = new[]
        {
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.StructWithInterface.CompareTo -> Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.StructWithInterface.Value",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.Extensions.Slice -> Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.ExtendedType.Data",
            "Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.PartialClient.CreateInstance -> Regression.SpecificBugs.global.Regression.SpecificBugs.RecordsAndStructs.PartialClient.OnCreated"
        };
        Assert.That(actual, Is.EquivalentTo(expected));
    }
}