using System;

namespace Core.MethodGroups;

// Test method groups in event handling scenarios
public class EventMethodGroups
{
    public event Action<string>? StringEvent;
    public event Func<int, bool>? ValidationEvent;
    public event EventHandler? LegacyEvent;

    public void SetupEventHandlers()
    {
        // Traditional event handler assignment (should work with existing code)
        StringEvent += HandleStringEvent;
        StringEvent += StaticStringHandler;

        // Event handler removal
        StringEvent -= HandleStringEvent;

        // Multiple handlers
        ValidationEvent += ValidatePositive;
        ValidationEvent += ValidateEven;
    }

    // Old-style registration via explicit delegate creation - should still create a Handles
    // relationship from the handler to the event.
    public void SetupLegacyHandler()
    {
        LegacyEvent += new EventHandler(OnLegacy);
    }

    private void OnLegacy(object? sender, EventArgs e)
    {
        Console.WriteLine("Legacy event handled");
    }

    public void TestEventInvocation()
    {
        // Invoke events
        StringEvent?.Invoke("test message");
        var isValid = ValidationEvent?.Invoke(42) ?? false;
    }

    private void HandleStringEvent(string message)
    {
        Console.WriteLine($"Event handled: {message}");
    }

    private static void StaticStringHandler(string message)
    {
        Console.WriteLine($"Static event handler: {message}");
    }

    private bool ValidatePositive(int number)
    {
        return number > 0;
    }

    private bool ValidateEven(int number)
    {
        return number % 2 == 0;
    }
}

// Test interface events
public interface IEventProvider
{
    event Action<string> MessageReceived;
}

public class EventProvider : IEventProvider
{
    public event Action<string>? MessageReceived;

    public void TriggerEvent(string message)
    {
        MessageReceived?.Invoke(message);
    }
}

public class EventConsumer
{
    private readonly IEventProvider _provider;

    public EventConsumer(IEventProvider provider)
    {
        _provider = provider;
        // Method group in event subscription
        _provider.MessageReceived += HandleMessage;
    }

    private void HandleMessage(string message)
    {
        Console.WriteLine($"Consumed: {message}");
    }
}