using System;

namespace Core.Events;

// Test different event invocation patterns (from original SampleProject)
public interface IEventProvider
{
    event EventHandler<EventArgs>? MyEvent;
}

public class EventInvoker : IEventProvider
{
    public event EventHandler<EventArgs>? MyEvent;

    // Various ways to invoke events
    public void InvokeMethod1()
    {
        // Null-conditional invocation
        MyEvent?.Invoke(this, EventArgs.Empty);
    }

    public void InvokeMethod2()
    {
        // Direct invocation (can throw NullReferenceException)
        MyEvent.Invoke(this, EventArgs.Empty);
    }

    public void InvokeMethod3()
    {
        // Direct call as delegate
        MyEvent(this, EventArgs.Empty);
    }

    public void InvokeMethod4()
    {
        // Traditional null check
        if (MyEvent != null)
        {
            MyEvent(this, EventArgs.Empty);
        }
    }

    public void InvokeMethod5()
    {
        // Copy to local variable
        var handler = MyEvent;
        handler?.Invoke(this, EventArgs.Empty);
    }

    // Method that triggers all invocation patterns
    public void TriggerAllInvocations()
    {
        InvokeMethod1();
        InvokeMethod2();
        InvokeMethod3();
        InvokeMethod4();
        InvokeMethod5();
    }
}

// Event sink that subscribes to interface events
public class EventSink
{
    public EventSink(IEventProvider provider)
    {
        provider.MyEvent += HandleEvent;
    }

    private void HandleEvent(object? sender, EventArgs e)
    {
        Console.WriteLine($"Event handled from {sender}");
    }
}

// Complex event chain
public class EventChain
{

    public EventChain()
    {
        // Chain events together
        FirstEvent += TriggerSecondEvent;
        SecondEvent += TriggerThirdEvent;
    }

    public event Action<string>? FirstEvent;
    public event Action<string>? SecondEvent;
    public event Action<string>? ThirdEvent;

    public void StartChain(string message)
    {
        FirstEvent?.Invoke(message);
    }

    private void TriggerSecondEvent(string message)
    {
        Console.WriteLine($"First event: {message}");
        SecondEvent?.Invoke($"Modified: {message}");
    }

    private void TriggerThirdEvent(string message)
    {
        Console.WriteLine($"Second event: {message}");
        ThirdEvent?.Invoke($"Final: {message}");
    }
}

// Event aggregation
public class EventAggregator
{
    private readonly object _lock = new();

    private int _eventCount;
    public event EventHandler<AggregatedEventArgs>? AggregatedEvent;

    public void Subscribe(EventPublisher publisher)
    {
        publisher.MessageEvent += HandleMessage;
        publisher.SimpleEvent += HandleSimple;
    }

    private void HandleMessage(string message)
    {
        lock (_lock)
        {
            _eventCount++;
            AggregatedEvent?.Invoke(this, new AggregatedEventArgs($"Message: {message}", _eventCount));
        }
    }

    private void HandleSimple()
    {
        lock (_lock)
        {
            _eventCount++;
            AggregatedEvent?.Invoke(this, new AggregatedEventArgs("Simple event", _eventCount));
        }
    }
}

public class AggregatedEventArgs : EventArgs
{

    public AggregatedEventArgs(string description, int eventCount)
    {
        Description = description;
        EventCount = eventCount;
    }

    public string Description { get; }
    public int EventCount { get; }
}

// Generic event handler
public class GenericEventHandler<T>
{
    public event EventHandler<GenericEventArgs<T>>? DataEvent;

    public void TriggerEvent(T data)
    {
        DataEvent?.Invoke(this, new GenericEventArgs<T>(data));
    }
}

public class GenericEventArgs<T> : EventArgs
{

    public GenericEventArgs(T data)
    {
        Data = data;
    }

    public T Data { get; }
}

// Conditional event subscription
public class ConditionalEventHandler
{
    private bool _isSubscribed;

    public void SubscribeConditionally(EventPublisher publisher, bool shouldSubscribe)
    {
        if (shouldSubscribe && !_isSubscribed)
        {
            publisher.MessageEvent += HandleMessage;
            _isSubscribed = true;
        }
        else if (!shouldSubscribe && _isSubscribed)
        {
            publisher.MessageEvent -= HandleMessage;
            _isSubscribed = false;
        }
    }

    private void HandleMessage(string message)
    {
        Console.WriteLine($"Conditionally handled: {message}");
    }
}