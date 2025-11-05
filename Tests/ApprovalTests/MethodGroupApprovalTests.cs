using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests;

/// <summary>
///     Focused approval tests for method group functionality.
/// </summary>
[TestFixture]
public class MethodGroupApprovalTests : ApprovalTestBase
{

    private CodeGraph.Graph.CodeGraph GetTestAssemblyGraph()
    {
        return GetTestGraph("Core.MethodGroups");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var actual = GetAllClasses(GetTestAssemblyGraph());

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands",
            "Core.MethodGroups.global.Core.MethodGroups.OtherClass",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups",
            "Core.MethodGroups.global.Core.MethodGroups.EventProvider",
            "Core.MethodGroups.global.Core.MethodGroups.EventConsumer",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void Methods_ShouldBeDetected()
    {
        var actual = GetAllNodesOfType(GetTestAssemblyGraph(), CodeElementType.Method);

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands..ctor",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.SetupCommands",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.HandleString",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.ValidateNumber",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.StaticHandler",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.ExecuteAction",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.ExecutePredicate",
            "Core.MethodGroups.global.Core.MethodGroups.OtherClass.InstanceMethod",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.SetupEventHandlers",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.TestEventInvocation",
            "Core.MethodGroups.global.Core.MethodGroups.EventConsumer..ctor",
            "Core.MethodGroups.global.Core.MethodGroups.EventConsumer.HandleMessage",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.HandleStringEvent",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.StaticStringHandler",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.ValidateEven",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.ValidatePositive",
            "Core.MethodGroups.global.Core.MethodGroups.EventProvider.TriggerEvent",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.DoubleNumber",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.IsEven",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.IsPositive",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.IsValidString",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.LogString",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TestDelegateAssignments",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TransformString"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodGroupUsages_ShouldBeDetected()
    {
        var actual = GetAllMethodGroupUsages(GetTestAssemblyGraph());

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.HandleString",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.ValidateNumber",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.StaticHandler",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.global.Core.MethodGroups.OtherClass.InstanceMethod",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.IsEven",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TransformString",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.IsValidString",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.DoubleNumber",
            "Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.global.Core.MethodGroups.LinqMethodGroups.IsPositive"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }



    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var callsRelationships = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls);

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.ExecuteAction",
            "Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.global.Core.MethodGroups.DelegateCommands.ExecutePredicate"
        };

        Assert.That(callsRelationships, Is.EquivalentTo(expected));

        // Note: This test verifies that method groups are NOT creating Calls relationships
        // They should create Uses relationships with IsMethodGroup attribute instead
        var methodGroupCalls = callsRelationships.Where(call =>
            call.Contains("HandleString") ||
            call.Contains("ValidateNumber") ||
            call.Contains("IsEven") ||
            call.Contains("TransformString")).ToList();

        Assert.That(methodGroupCalls.Count == 0);
    }

    [Test]
    public void EventSubscriptions_ShouldBeDetected()
    {
        var actual = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Handles);

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.HandleStringEvent -> Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.StringEvent",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.StaticStringHandler -> Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.StringEvent",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.ValidatePositive -> Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.ValidationEvent",
            "Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.ValidateEven -> Core.MethodGroups.global.Core.MethodGroups.EventMethodGroups.ValidationEvent",
            "Core.MethodGroups.global.Core.MethodGroups.EventConsumer.HandleMessage -> Core.MethodGroups.global.Core.MethodGroups.IEventProvider.MessageReceived"
        };

        // Note: Event handlers use existing += syntax which already works
        // This test verifies the existing functionality still works
        Assert.That(actual, Is.EquivalentTo(expected));
    }
}