using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Method groups passed to delegate constructors / as arguments, assigned to fields and locals, and
///     returned from a method. They produce "Uses" edges with the IsMethodGroup attribute, while the real
///     delegate-consuming calls stay "Calls". Migrated from the former Core.MethodGroups approval fixture
///     (DelegateCommands.cs).
/// </summary>
[TestFixture]
public class MethodGroups_DelegatesParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                     using System;

                                     namespace Demo;

                                     public class DelegateCommands
                                     {
                                         private Func<int, bool>? _intPredicate;
                                         private Action<string>? _stringCommand;

                                         public DelegateCommands()
                                         {
                                             _stringCommand = HandleString;
                                             _intPredicate = ValidateNumber;
                                             var directAction = HandleString;
                                             var directFunc = ValidateNumber;
                                         }

                                         public void SetupCommands()
                                         {
                                             ExecuteAction(HandleString);
                                             ExecutePredicate(ValidateNumber);
                                             ExecuteAction(StaticHandler);
                                             var other = new OtherClass();
                                             ExecuteAction(other.InstanceMethod);
                                         }

                                         public Action<string> GetCommand() { return HandleString; }

                                         private void HandleString(string input) { Console.WriteLine($"Handled: {input}"); }
                                         private bool ValidateNumber(int number) { return number > 0; }
                                         private static void StaticHandler(string input) { Console.WriteLine($"Static handled: {input}"); }
                                         private void ExecuteAction(Action<string> action) { action?.Invoke("test"); }
                                         private void ExecutePredicate(Func<int, bool> predicate) { predicate?.Invoke(42); }
                                     }

                                     public class OtherClass
                                     {
                                         public void InstanceMethod(string input) { Console.WriteLine($"Other instance: {input}"); }
                                     }
                                     """;

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(new[] { "DelegateCommands", "OtherClass" }));
    }

    [Test]
    public void Methods_AreDetected()
    {
        var expected = new[]
        {
            "DelegateCommands..ctor", "DelegateCommands.SetupCommands", "DelegateCommands.HandleString",
            "DelegateCommands.ValidateNumber", "DelegateCommands.StaticHandler", "DelegateCommands.ExecuteAction",
            "DelegateCommands.ExecutePredicate", "DelegateCommands.GetCommand",
            "OtherClass.InstanceMethod"
        };

        Assert.That(PathsOf(CodeElementType.Method), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodGroupUsages_AreDetected()
    {
        var expected = new[]
        {
            "DelegateCommands..ctor -> DelegateCommands.HandleString",
            "DelegateCommands..ctor -> DelegateCommands.ValidateNumber",
            "DelegateCommands.SetupCommands -> DelegateCommands.HandleString",
            "DelegateCommands.SetupCommands -> DelegateCommands.ValidateNumber",
            "DelegateCommands.SetupCommands -> DelegateCommands.StaticHandler",
            "DelegateCommands.SetupCommands -> OtherClass.InstanceMethod",
            "DelegateCommands.GetCommand -> DelegateCommands.HandleString"
        };

        Assert.That(MethodGroupUsages(), Is.EquivalentTo(expected));
    }

    [Test]
    public void RealDelegateConsumingCalls_AreCalls()
    {
        var expected = new[]
        {
            "DelegateCommands.SetupCommands -> DelegateCommands.ExecuteAction",
            "DelegateCommands.SetupCommands -> DelegateCommands.ExecutePredicate"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
