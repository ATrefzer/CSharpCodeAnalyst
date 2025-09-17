using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class NamespacesApprovalTests : ProjectTestBase
{
    private CodeGraph GetTestGraph()
    {
        return GetGraph("Core.Namespaces");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Core.Namespaces.Core.Namespaces.Level1.Level1Class",
            "Core.Namespaces.Core.Namespaces.Level1.AnotherLevel1Class",
            "Core.Namespaces.Core.Namespaces.Level1.Level2.Level2Class",
            "Core.Namespaces.Core.Namespaces.Level1.Level2.Level2Processor",
            "Core.Namespaces.Core.Namespaces.Level1.Level2.Level3.Level3Class",
            "Core.Namespaces.Core.Namespaces.Level1.Level2.Level3.DeepestClass",
            "Core.Namespaces.Core.Namespaces.RootClass"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    [Test]
    public void Usages_ShouldBeDetected()
    {
        var crossNamespaceUsages = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Uses);


        var expected = new[]
        {
            "Core.Namespaces.Core.Namespaces.Level1.Level1Class._level2 -> Core.Namespaces.Core.Namespaces.Level1.Level2.Level2Class",
            "Core.Namespaces.Core.Namespaces.Level1.Level1Class.DoSomething -> Core.Namespaces.Core.Namespaces.Level1.Level1Class._level2",
            "Core.Namespaces.Core.Namespaces.Level1.Level1Class.CreateLevel2 -> Core.Namespaces.Core.Namespaces.Level1.Level1Class._level2"
        };

        CollectionAssert.AreEquivalent(expected, crossNamespaceUsages);
    }

    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var methodCalls = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);


        var expected = new[]
        {
            "Core.Namespaces.Core.Namespaces.Level1.Level1Class.DoSomething -> Core.Namespaces.Core.Namespaces.Level1.Level2.Level2Class.ProcessData",
            "Core.Namespaces.Core.Namespaces.Level1.AnotherLevel1Class.WorkWithRoot -> Core.Namespaces.Core.Namespaces.RootClass.UseLevel1",
            "Core.Namespaces.Core.Namespaces.Level1.Level2.Level2Class.ProcessData -> Core.Namespaces.Core.Namespaces.Level1.Level2.Level3.Level3Class.DeepOperation",
            "Core.Namespaces.Core.Namespaces.Level1.Level2.Level2Processor.ProcessWithLevel1 -> Core.Namespaces.Core.Namespaces.Level1.Level1Class.DoSomething",
            "Core.Namespaces.Core.Namespaces.Level1.Level2.Level3.Level3Class.DeepOperation -> Core.Namespaces.Core.Namespaces.RootClass.UseLevel1",
            "Core.Namespaces.Core.Namespaces.Level1.Level2.Level3.DeepestClass.ReachToTop -> Core.Namespaces.Core.Namespaces.Level1.Level1Class.DoSomething",
            "Core.Namespaces.Core.Namespaces.RootClass.UseLevel1 -> Core.Namespaces.Core.Namespaces.Level1.Level1Class.DoSomething",
            "Core.Namespaces.Core.Namespaces.RootClass.UseLevel2 -> Core.Namespaces.Core.Namespaces.Level1.Level2.Level2Class.ProcessData",
            "Core.Namespaces.Core.Namespaces.RootClass.UseLevel3 -> Core.Namespaces.Core.Namespaces.Level1.Level2.Level3.Level3Class.DeepOperation"
        };

        CollectionAssert.AreEquivalent(expected, methodCalls);
    }
}