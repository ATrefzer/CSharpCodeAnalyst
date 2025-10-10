using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class NamespacesApprovalTests : ProjectTestBase
{
    private CodeGraph GetTestAssemblyGraph()
    {
        return GetAssemblyGraph("Core.Namespaces");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestAssemblyGraph()).ToList();

        var expected = new[]
        {
            "Core.Namespaces.global.Core.Namespaces.Level1.Level1Class",
            "Core.Namespaces.global.Core.Namespaces.Level1.AnotherLevel1Class",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Class",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Processor",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.Level3Class",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.DeepestClass",
            "Core.Namespaces.global.Core.Namespaces.RootClass"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    [Test]
    public void Usages_ShouldBeDetected()
    {
        var crossNamespaceUsages = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Uses);


        var expected = new[]
        {
            "Core.Namespaces.global.Core.Namespaces.Level1.Level1Class._level2 -> Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Class",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level1Class.DoSomething -> Core.Namespaces.global.Core.Namespaces.Level1.Level1Class._level2",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level1Class.CreateLevel2 -> Core.Namespaces.global.Core.Namespaces.Level1.Level1Class._level2",


            // Local declarations
            "Core.Namespaces.global.Core.Namespaces.Level1.AnotherLevel1Class.WorkWithRoot -> Core.Namespaces.global.Core.Namespaces.RootClass",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Class.ProcessData -> Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.Level3Class",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Processor.ProcessWithLevel1 -> Core.Namespaces.global.Core.Namespaces.Level1.Level1Class",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.DeepestClass.ReachToTop -> Core.Namespaces.global.Core.Namespaces.Level1.Level1Class",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.Level3Class.DeepOperation -> Core.Namespaces.global.Core.Namespaces.RootClass",
            "Core.Namespaces.global.Core.Namespaces.RootClass.UseLevel1 -> Core.Namespaces.global.Core.Namespaces.Level1.Level1Class",
            "Core.Namespaces.global.Core.Namespaces.RootClass.UseLevel2 -> Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Class",
            "Core.Namespaces.global.Core.Namespaces.RootClass.UseLevel3 -> Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.Level3Class"
        };

        CollectionAssert.AreEquivalent(expected, crossNamespaceUsages);
    }

    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var methodCalls = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls);


        var expected = new[]
        {
            "Core.Namespaces.global.Core.Namespaces.Level1.Level1Class.DoSomething -> Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Class.ProcessData",
            "Core.Namespaces.global.Core.Namespaces.Level1.AnotherLevel1Class.WorkWithRoot -> Core.Namespaces.global.Core.Namespaces.RootClass.UseLevel1",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Class.ProcessData -> Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.Level3Class.DeepOperation",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Processor.ProcessWithLevel1 -> Core.Namespaces.global.Core.Namespaces.Level1.Level1Class.DoSomething",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.Level3Class.DeepOperation -> Core.Namespaces.global.Core.Namespaces.RootClass.UseLevel1",
            "Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.DeepestClass.ReachToTop -> Core.Namespaces.global.Core.Namespaces.Level1.Level1Class.DoSomething",
            "Core.Namespaces.global.Core.Namespaces.RootClass.UseLevel1 -> Core.Namespaces.global.Core.Namespaces.Level1.Level1Class.DoSomething",
            "Core.Namespaces.global.Core.Namespaces.RootClass.UseLevel2 -> Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level2Class.ProcessData",
            "Core.Namespaces.global.Core.Namespaces.RootClass.UseLevel3 -> Core.Namespaces.global.Core.Namespaces.Level1.Level2.Level3.Level3Class.DeepOperation"
        };

        CollectionAssert.AreEquivalent(expected, methodCalls);
    }
}