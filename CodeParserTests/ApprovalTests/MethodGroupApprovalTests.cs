using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

/// <summary>
///     Focused approval tests for method group functionality.
/// </summary>
[TestFixture]
public class MethodGroupApprovalTests : ProjectTestBase
{

    private CodeGraph GetTestAssemblyGraph()
    {
        return GetAssemblyGraph("Core.MethodGroups");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var actual = GetAllClasses(GetTestAssemblyGraph());

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands",
            "Core.MethodGroups.Core.MethodGroups.OtherClass",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups",
            "Core.MethodGroups.Core.MethodGroups.EventProvider",
            "Core.MethodGroups.Core.MethodGroups.EventConsumer",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void Methods_ShouldBeDetected()
    {
        var actual = GetAllNodesOfType(GetTestAssemblyGraph(), CodeElementType.Method);

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands..ctor",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.SetupCommands",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.HandleString",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.ValidateNumber",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.StaticHandler",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.ExecuteAction",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.ExecutePredicate",
            "Core.MethodGroups.Core.MethodGroups.OtherClass.InstanceMethod",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.SetupEventHandlers",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.TestEventInvocation",
            "Core.MethodGroups.Core.MethodGroups.EventConsumer..ctor",
            "Core.MethodGroups.Core.MethodGroups.EventConsumer.HandleMessage",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.HandleStringEvent",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.StaticStringHandler",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.ValidateEven",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.ValidatePositive",
            "Core.MethodGroups.Core.MethodGroups.EventProvider.TriggerEvent",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.DoubleNumber",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.IsEven",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.IsPositive",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.IsValidString",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.LogString",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TestDelegateAssignments",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TransformString"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void MethodGroupUsages_ShouldBeDetected()
    {
        var actual = GetAllMethodGroupUsages(GetTestAssemblyGraph());

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.Core.MethodGroups.DelegateCommands.HandleString",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.Core.MethodGroups.DelegateCommands.ValidateNumber",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.Core.MethodGroups.DelegateCommands.StaticHandler",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.Core.MethodGroups.OtherClass.InstanceMethod",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.IsEven",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TransformString",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.IsValidString",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.DoubleNumber",
            "Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.TestLinqWithMethodGroups -> Core.MethodGroups.Core.MethodGroups.LinqMethodGroups.IsPositive"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }



    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var callsRelationships = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls);

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.Core.MethodGroups.DelegateCommands.ExecuteAction",
            "Core.MethodGroups.Core.MethodGroups.DelegateCommands.SetupCommands -> Core.MethodGroups.Core.MethodGroups.DelegateCommands.ExecutePredicate"
        };

        CollectionAssert.AreEquivalent(expected, callsRelationships);

        // Note: This test verifies that method groups are NOT creating Calls relationships
        // They should create Uses relationships with IsMethodGroup attribute instead
        var methodGroupCalls = callsRelationships.Where(call =>
            call.Contains("HandleString") ||
            call.Contains("ValidateNumber") ||
            call.Contains("IsEven") ||
            call.Contains("TransformString")).ToList();

        Assert.IsEmpty(methodGroupCalls,
            $"Method groups should not create Calls relationships. Found: {string.Join("", methodGroupCalls)}");
    }

    [Test]
    public void EventSubscriptions_ShouldBeDetected()
    {
        var actual = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Handles);

        var expected = new HashSet<string>
        {
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.HandleStringEvent -> Core.MethodGroups.Core.MethodGroups.EventMethodGroups.StringEvent",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.StaticStringHandler -> Core.MethodGroups.Core.MethodGroups.EventMethodGroups.StringEvent",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.ValidatePositive -> Core.MethodGroups.Core.MethodGroups.EventMethodGroups.ValidationEvent",
            "Core.MethodGroups.Core.MethodGroups.EventMethodGroups.ValidateEven -> Core.MethodGroups.Core.MethodGroups.EventMethodGroups.ValidationEvent",
            "Core.MethodGroups.Core.MethodGroups.EventConsumer.HandleMessage -> Core.MethodGroups.Core.MethodGroups.IEventProvider.MessageReceived"
        };

        // Note: Event handlers use existing += syntax which already works
        // This test verifies the existing functionality still works
        CollectionAssert.AreEquivalent(expected, actual);
    }
}