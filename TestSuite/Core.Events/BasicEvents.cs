using System;

namespace Core.Events;

// Test basic event declarations and usage
public class EventPublisher
{
    // Simple event
    public event Action? SimpleEvent;

    // Event with parameters
    public event Action<string>? MessageEvent;

    // Event with custom EventArgs
    public event EventHandler<CustomEventArgs>? CustomEvent;

    // Event with generic EventArgs
    public event EventHandler<DataChangedEventArgs<int>>? DataChanged;

    public void TriggerSimpleEvent()
    {
        SimpleEvent?.Invoke();
    }

    public void TriggerMessageEvent(string message)
    {
        MessageEvent?.Invoke(message);
    }

    public void TriggerCustomEvent(string data)
    {
        CustomEvent?.Invoke(this, new CustomEventArgs(data));
    }

    public void TriggerDataChanged(int oldValue, int newValue)
    {
        DataChanged?.Invoke(this, new DataChangedEventArgs<int>(oldValue, newValue));
    }

    // Different event invocation patterns
    public void AlternativeInvocations(string message)
    {
        // Direct invocation
        MessageEvent?.Invoke(message);

        // Using Invoke method explicitly
        MessageEvent.Invoke(message);

        // Traditional invocation check
        if (MessageEvent != null)
        {
            MessageEvent(message);
        }
    }
}

// Custom EventArgs
public class CustomEventArgs : EventArgs
{

    public CustomEventArgs(string data)
    {
        Data = data;
    }

    public string Data { get; }
}

// Generic EventArgs
public class DataChangedEventArgs<T> : EventArgs
{

    public DataChangedEventArgs(T oldValue, T newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public T OldValue { get; }
    public T NewValue { get; }
}

// Event subscriber
public class EventSubscriber
{
    private readonly EventPublisher _publisher;

    public EventSubscriber(EventPublisher publisher)
    {
        _publisher = publisher;
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        // Event subscription with method groups
        _publisher.SimpleEvent += HandleSimpleEvent;
        _publisher.MessageEvent += HandleMessageEvent;
        _publisher.CustomEvent += HandleCustomEvent;
        _publisher.DataChanged += HandleDataChanged;

        // Static method subscription
        _publisher.MessageEvent += StaticMessageHandler;
    }

    public void UnsubscribeFromEvents()
    {
        // Event unsubscription
        _publisher.SimpleEvent -= HandleSimpleEvent;
        _publisher.MessageEvent -= HandleMessageEvent;
        _publisher.CustomEvent -= HandleCustomEvent;
        _publisher.DataChanged -= HandleDataChanged;
        _publisher.MessageEvent -= StaticMessageHandler;
    }

    private void HandleSimpleEvent()
    {
        Console.WriteLine("Simple event handled");
    }

    private void HandleMessageEvent(string message)
    {
        Console.WriteLine($"Message received: {message}");
    }

    private void HandleCustomEvent(object? sender, CustomEventArgs e)
    {
        Console.WriteLine($"Custom event from {sender}: {e.Data}");
    }

    private void HandleDataChanged(object? sender, DataChangedEventArgs<int> e)
    {
        Console.WriteLine($"Data changed from {e.OldValue} to {e.NewValue}");
    }

    private static void StaticMessageHandler(string message)
    {
        Console.WriteLine($"Static handler: {message}");
    }
}

// Multiple event subscribers
public class EventLogger
{
    public EventLogger(EventPublisher publisher)
    {
        publisher.MessageEvent += LogMessage;
        publisher.CustomEvent += LogCustomEvent;
    }

    private void LogMessage(string message)
    {
        Console.WriteLine($"[LOG] Message: {message}");
    }

    private void LogCustomEvent(object? sender, CustomEventArgs e)
    {
        Console.WriteLine($"[LOG] Custom event: {e.Data}");
    }
}

public class EventCounter
{
    private int _eventCount;

    public EventCounter(EventPublisher publisher)
    {
        publisher.SimpleEvent += CountEvent;
        publisher.MessageEvent += CountMessageEvent;
    }

    private void CountEvent()
    {
        _eventCount++;
    }

    private void CountMessageEvent(string message)
    {
        _eventCount++;
    }

    public int GetEventCount()
    {
        return _eventCount;
    }
}