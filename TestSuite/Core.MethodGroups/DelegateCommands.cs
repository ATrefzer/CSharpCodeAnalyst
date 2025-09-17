using System;

namespace Core.MethodGroups;

// Test method groups passed to delegate constructors
public class DelegateCommands
{
    private Func<int, bool>? _intPredicate;
    private Action<string>? _stringCommand;

    public DelegateCommands()
    {
        // Method group in constructor argument - should create Uses relationship
        _stringCommand = HandleString;
        _intPredicate = ValidateNumber;

        // Method group direct assignment
        var directAction = HandleString;
        var directFunc = ValidateNumber;
    }

    public void SetupCommands()
    {
        // Method groups in method calls
        ExecuteAction(HandleString);
        ExecutePredicate(ValidateNumber);

        // Static method groups
        ExecuteAction(StaticHandler);

        // Instance method groups from other objects
        var other = new OtherClass();
        ExecuteAction(other.InstanceMethod);
    }

    private void HandleString(string input)
    {
        Console.WriteLine($"Handled: {input}");
    }

    private bool ValidateNumber(int number)
    {
        return number > 0;
    }

    private static void StaticHandler(string input)
    {
        Console.WriteLine($"Static handled: {input}");
    }

    private void ExecuteAction(Action<string> action)
    {
        action?.Invoke("test");
    }

    private void ExecutePredicate(Func<int, bool> predicate)
    {
        predicate?.Invoke(42);
    }
}

public class OtherClass
{
    public void InstanceMethod(string input)
    {
        Console.WriteLine($"Other instance: {input}");
    }
}