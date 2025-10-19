using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class GenericsApprovalTests : ApprovalTestBase
{
    private CodeGraph GetTestAssemblyGraph()
    {
        return GetAssemblyGraph("Core.Generics");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.Generics.global.Core.Generics.GenericProcessor",
            "Core.Generics.global.Core.Generics.GenericCalculator",
            "Core.Generics.global.Core.Generics.GenericManager",
            "Core.Generics.global.Core.Generics.GenericFactory",
            "Core.Generics.global.Core.Generics.GenericSorter",
            "Core.Generics.global.Core.Generics.BaseEntity",
            "Core.Generics.global.Core.Generics.EntityManager",
            "Core.Generics.global.Core.Generics.GenericConverter",
            "Core.Generics.global.Core.Generics.ProcessableItem",
            "Core.Generics.global.Core.Generics.ComparableItem",
            "Core.Generics.global.Core.Generics.DatabaseEntity",
            "Core.Generics.global.Core.Generics.GenericCollection",
            "Core.Generics.global.Core.Generics.GenericContainer",
            "Core.Generics.global.Core.Generics.GenericCreator",
            "Core.Generics.global.Core.Generics.GenericMethodsClass",
            "Core.Generics.global.Core.Generics.GenericPair",
            "Core.Generics.global.Core.Generics.GenericService",
            "Core.Generics.global.Core.Generics.GenericTree",
            "Core.Generics.global.Core.Generics.GenericTree.Node",
            "Core.Generics.global.Core.Generics.GenericUtilities",
            "Core.Generics.global.Core.Generics.NumberValidator",
            "Core.Generics.global.Core.Generics.StringValidator"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    [Test]
    public void GenericConstraints_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestAssemblyGraph())
            .Where(c => c.Contains("Constraint") || c.Contains("Comparable"))
            .OrderBy(c => c)
            .ToList();

        var expected = new[]
        {
            "Core.Generics.global.Core.Generics.ComparableItem"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var callRelationships = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls);

        var expected = new[]
        {
            "Core.Generics.global.Core.Generics.GenericManager.ProcessAll -> Core.Generics.global.Core.Generics.IProcessor.Process",
            "Core.Generics.global.Core.Generics.EntityManager.AddEntity -> Core.Generics.global.Core.Generics.BaseEntity.Id",
            "Core.Generics.global.Core.Generics.EntityManager.SaveAll -> Core.Generics.global.Core.Generics.BaseEntity.Save",
            "Core.Generics.global.Core.Generics.GenericConverter.ConvertMany -> Core.Generics.global.Core.Generics.GenericConverter.Convert",
            "Core.Generics.global.Core.Generics.ProcessableItem.Process -> Core.Generics.global.Core.Generics.ProcessableItem.Name",
            "Core.Generics.global.Core.Generics.ComparableItem.CompareTo -> Core.Generics.global.Core.Generics.ComparableItem.Value",
            "Core.Generics.global.Core.Generics.DatabaseEntity.Save -> Core.Generics.global.Core.Generics.BaseEntity.Id",
            "Core.Generics.global.Core.Generics.DatabaseEntity.Save -> Core.Generics.global.Core.Generics.DatabaseEntity.Name",
            "Core.Generics.global.Core.Generics.GenericMethodsClass.ProcessItems -> Core.Generics.global.Core.Generics.IProcessor.Process",
            "Core.Generics.global.Core.Generics.GenericMethodsClass.SaveEntities -> Core.Generics.global.Core.Generics.BaseEntity.Save",
            "Core.Generics.global.Core.Generics.GenericPair..ctor -> Core.Generics.global.Core.Generics.GenericPair.First",
            "Core.Generics.global.Core.Generics.GenericPair..ctor -> Core.Generics.global.Core.Generics.GenericPair.Second",
            "Core.Generics.global.Core.Generics.GenericPair.Swap -> Core.Generics.global.Core.Generics.GenericPair.First",
            "Core.Generics.global.Core.Generics.GenericTree.Node..ctor -> Core.Generics.global.Core.Generics.GenericTree.Node.Value",
            "Core.Generics.global.Core.Generics.GenericTree.Node.AddChild -> Core.Generics.global.Core.Generics.GenericTree.Node.Children",
            "Core.Generics.global.Core.Generics.GenericTree.SetRoot -> Core.Generics.global.Core.Generics.GenericTree.Root",

            "Core.Generics.global.Core.Generics.GenericCreator.CreateContainer -> Core.Generics.global.Core.Generics.GenericContainer..ctor",
            "Core.Generics.global.Core.Generics.GenericCreator.CreatePair -> Core.Generics.global.Core.Generics.GenericPair..ctor",
            "Core.Generics.global.Core.Generics.GenericService.WrapResult -> Core.Generics.global.Core.Generics.GenericContainer..ctor",
            "Core.Generics.global.Core.Generics.GenericTree.Node.AddChild -> Core.Generics.global.Core.Generics.GenericTree.Node..ctor",
            "Core.Generics.global.Core.Generics.GenericTree.SetRoot -> Core.Generics.global.Core.Generics.GenericTree.Node..ctor",
            "Core.Generics.global.Core.Generics.GenericUtilities.MakePair -> Core.Generics.global.Core.Generics.GenericPair..ctor",

            "Core.Generics.global.Core.Generics.GenericService.ProcessWithValidator -> Core.Generics.global.Core.Generics.IValidator.IsValid"
            // No longer supported because it is inside a lambda!
            // "Core.Generics.global.Core.Generics.GenericSorter.Sort -> Core.Generics.global.Core.Generics.IComparable.CompareTo",
        };

        CollectionAssert.AreEquivalent(expected, callRelationships.ToArray());
    }
}