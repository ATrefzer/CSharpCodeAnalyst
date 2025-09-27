using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class GenericsApprovalTests : ProjectTestBase
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
            "Core.Generics.Core.Generics.GenericProcessor",
            "Core.Generics.Core.Generics.GenericCalculator",
            "Core.Generics.Core.Generics.GenericManager",
            "Core.Generics.Core.Generics.GenericFactory",
            "Core.Generics.Core.Generics.GenericSorter",
            "Core.Generics.Core.Generics.BaseEntity",
            "Core.Generics.Core.Generics.EntityManager",
            "Core.Generics.Core.Generics.GenericConverter",
            "Core.Generics.Core.Generics.ProcessableItem",
            "Core.Generics.Core.Generics.ComparableItem",
            "Core.Generics.Core.Generics.DatabaseEntity",
            "Core.Generics.Core.Generics.GenericCollection",
            "Core.Generics.Core.Generics.GenericContainer",
            "Core.Generics.Core.Generics.GenericCreator",
            "Core.Generics.Core.Generics.GenericMethodsClass",
            "Core.Generics.Core.Generics.GenericPair",
            "Core.Generics.Core.Generics.GenericService",
            "Core.Generics.Core.Generics.GenericTree",
            "Core.Generics.Core.Generics.GenericTree.Node",
            "Core.Generics.Core.Generics.GenericUtilities",
            "Core.Generics.Core.Generics.NumberValidator",
            "Core.Generics.Core.Generics.StringValidator"
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
            "Core.Generics.Core.Generics.ComparableItem"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var callRelationships = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls)
            ;

        var expected = new[]
        {
            "Core.Generics.Core.Generics.GenericManager.ProcessAll -> Core.Generics.Core.Generics.IProcessor.Process",
            "Core.Generics.Core.Generics.GenericSorter.Sort -> Core.Generics.Core.Generics.IComparable.CompareTo",
            "Core.Generics.Core.Generics.EntityManager.AddEntity -> Core.Generics.Core.Generics.BaseEntity.Id",
            "Core.Generics.Core.Generics.EntityManager.SaveAll -> Core.Generics.Core.Generics.BaseEntity.Save",
            "Core.Generics.Core.Generics.GenericConverter.ConvertMany -> Core.Generics.Core.Generics.GenericConverter.Convert",
            "Core.Generics.Core.Generics.ProcessableItem.Process -> Core.Generics.Core.Generics.ProcessableItem.Name",
            "Core.Generics.Core.Generics.ComparableItem.CompareTo -> Core.Generics.Core.Generics.ComparableItem.Value",
            "Core.Generics.Core.Generics.DatabaseEntity.Save -> Core.Generics.Core.Generics.BaseEntity.Id",
            "Core.Generics.Core.Generics.DatabaseEntity.Save -> Core.Generics.Core.Generics.DatabaseEntity.Name",
            "Core.Generics.Core.Generics.GenericMethodsClass.ProcessItems -> Core.Generics.Core.Generics.IProcessor.Process",
            "Core.Generics.Core.Generics.GenericMethodsClass.SaveEntities -> Core.Generics.Core.Generics.BaseEntity.Save",
            "Core.Generics.Core.Generics.GenericPair..ctor -> Core.Generics.Core.Generics.GenericPair.First",
            "Core.Generics.Core.Generics.GenericPair..ctor -> Core.Generics.Core.Generics.GenericPair.Second",
            "Core.Generics.Core.Generics.GenericPair.Swap -> Core.Generics.Core.Generics.GenericPair.First",
            "Core.Generics.Core.Generics.GenericTree.Node..ctor -> Core.Generics.Core.Generics.GenericTree.Node.Value",
            "Core.Generics.Core.Generics.GenericTree.Node.AddChild -> Core.Generics.Core.Generics.GenericTree.Node.Children",
            "Core.Generics.Core.Generics.GenericTree.SetRoot -> Core.Generics.Core.Generics.GenericTree.Root",

            "Core.Generics.Core.Generics.GenericCreator.CreateContainer -> Core.Generics.Core.Generics.GenericContainer..ctor",
            "Core.Generics.Core.Generics.GenericCreator.CreatePair -> Core.Generics.Core.Generics.GenericPair..ctor",
            "Core.Generics.Core.Generics.GenericService.WrapResult -> Core.Generics.Core.Generics.GenericContainer..ctor",
            "Core.Generics.Core.Generics.GenericTree.Node.AddChild -> Core.Generics.Core.Generics.GenericTree.Node..ctor",
            "Core.Generics.Core.Generics.GenericTree.SetRoot -> Core.Generics.Core.Generics.GenericTree.Node..ctor",
            "Core.Generics.Core.Generics.GenericUtilities.MakePair -> Core.Generics.Core.Generics.GenericPair..ctor"
        };

        CollectionAssert.AreEquivalent(expected, callRelationships.ToArray());
    }
}