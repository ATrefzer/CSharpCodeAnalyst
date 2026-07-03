using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Event declarations, subscription/unsubscription (Handles), the various invocation patterns and the
///     EventArgs constructor/property call edges. Covers the BasicEvents and EventInvocation source files
///     together because EventInvocation's EventAggregator / ConditionalEventHandler take an EventPublisher
///     (declared in BasicEvents) as a parameter. Migrated from the former Core.Events approval fixture.
/// </summary>
[TestFixture]
public class Events_PublishSubscribeParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                     using System;

                                     namespace Demo;

                                     public class EventPublisher
                                     {
                                         public event Action? SimpleEvent;
                                         public event Action<string>? MessageEvent;
                                         public event EventHandler<CustomEventArgs>? CustomEvent;
                                         public event EventHandler<DataChangedEventArgs<int>>? DataChanged;

                                         public void TriggerSimpleEvent() { SimpleEvent?.Invoke(); }
                                         public void TriggerMessageEvent(string message) { MessageEvent?.Invoke(message); }
                                         public void TriggerCustomEvent(string data) { CustomEvent?.Invoke(this, new CustomEventArgs(data)); }
                                         public void TriggerDataChanged(int oldValue, int newValue) { DataChanged?.Invoke(this, new DataChangedEventArgs<int>(oldValue, newValue)); }

                                         public void AlternativeInvocations(string message)
                                         {
                                             MessageEvent?.Invoke(message);
                                             MessageEvent.Invoke(message);
                                             if (MessageEvent != null) { MessageEvent(message); }
                                         }
                                     }

                                     public class CustomEventArgs : EventArgs
                                     {
                                         public CustomEventArgs(string data) { Data = data; }
                                         public string Data { get; }
                                     }

                                     public class DataChangedEventArgs<T> : EventArgs
                                     {
                                         public DataChangedEventArgs(T oldValue, T newValue) { OldValue = oldValue; NewValue = newValue; }
                                         public T OldValue { get; }
                                         public T NewValue { get; }
                                     }

                                     public class EventSubscriber
                                     {
                                         private readonly EventPublisher _publisher;
                                         public EventSubscriber(EventPublisher publisher) { _publisher = publisher; SubscribeToEvents(); }

                                         private void SubscribeToEvents()
                                         {
                                             _publisher.SimpleEvent += HandleSimpleEvent;
                                             _publisher.MessageEvent += HandleMessageEvent;
                                             _publisher.CustomEvent += HandleCustomEvent;
                                             _publisher.DataChanged += HandleDataChanged;
                                             _publisher.MessageEvent += StaticMessageHandler;
                                         }

                                         public void UnsubscribeFromEvents()
                                         {
                                             _publisher.SimpleEvent -= HandleSimpleEvent;
                                             _publisher.MessageEvent -= HandleMessageEvent;
                                             _publisher.CustomEvent -= HandleCustomEvent;
                                             _publisher.DataChanged -= HandleDataChanged;
                                             _publisher.MessageEvent -= StaticMessageHandler;
                                         }

                                         private void HandleSimpleEvent() { Console.WriteLine("Simple event handled"); }
                                         private void HandleMessageEvent(string message) { Console.WriteLine($"Message received: {message}"); }
                                         private void HandleCustomEvent(object? sender, CustomEventArgs e) { Console.WriteLine($"Custom event from {sender}: {e.Data}"); }
                                         private void HandleDataChanged(object? sender, DataChangedEventArgs<int> e) { Console.WriteLine($"Data changed from {e.OldValue} to {e.NewValue}"); }
                                         private static void StaticMessageHandler(string message) { Console.WriteLine($"Static handler: {message}"); }
                                     }

                                     public class EventLogger
                                     {
                                         public EventLogger(EventPublisher publisher) { publisher.MessageEvent += LogMessage; publisher.CustomEvent += LogCustomEvent; }
                                         private void LogMessage(string message) { Console.WriteLine($"[LOG] Message: {message}"); }
                                         private void LogCustomEvent(object? sender, CustomEventArgs e) { Console.WriteLine($"[LOG] Custom event: {e.Data}"); }
                                     }

                                     public class EventCounter
                                     {
                                         private int _eventCount;
                                         public EventCounter(EventPublisher publisher) { publisher.SimpleEvent += CountEvent; publisher.MessageEvent += CountMessageEvent; }
                                         private void CountEvent() { _eventCount++; }
                                         private void CountMessageEvent(string message) { _eventCount++; }
                                         public int GetEventCount() { return _eventCount; }
                                     }

                                     public interface IEventProvider { event EventHandler<EventArgs>? MyEvent; }

                                     public class EventInvoker : IEventProvider
                                     {
                                         public event EventHandler<EventArgs>? MyEvent;
                                         public void InvokeMethod1() { MyEvent?.Invoke(this, EventArgs.Empty); }
                                         public void InvokeMethod2() { MyEvent.Invoke(this, EventArgs.Empty); }
                                         public void InvokeMethod3() { MyEvent(this, EventArgs.Empty); }
                                         public void InvokeMethod4() { if (MyEvent != null) { MyEvent(this, EventArgs.Empty); } }
                                         public void InvokeMethod5() { var handler = MyEvent; handler?.Invoke(this, EventArgs.Empty); }
                                         public void TriggerAllInvocations() { InvokeMethod1(); InvokeMethod2(); InvokeMethod3(); InvokeMethod4(); InvokeMethod5(); }
                                     }

                                     public class EventSink
                                     {
                                         public EventSink(IEventProvider provider) { provider.MyEvent += HandleEvent; }
                                         private void HandleEvent(object? sender, EventArgs e) { Console.WriteLine($"Event handled from {sender}"); }
                                     }

                                     public class EventChain
                                     {
                                         public EventChain() { FirstEvent += TriggerSecondEvent; SecondEvent += TriggerThirdEvent; }
                                         public event Action<string>? FirstEvent;
                                         public event Action<string>? SecondEvent;
                                         public event Action<string>? ThirdEvent;
                                         public void StartChain(string message) { FirstEvent?.Invoke(message); }
                                         private void TriggerSecondEvent(string message) { Console.WriteLine($"First event: {message}"); SecondEvent?.Invoke($"Modified: {message}"); }
                                         private void TriggerThirdEvent(string message) { Console.WriteLine($"Second event: {message}"); ThirdEvent?.Invoke($"Final: {message}"); }
                                     }

                                     public class EventAggregator
                                     {
                                         private readonly object _lock = new();
                                         private int _eventCount;
                                         public event EventHandler<AggregatedEventArgs>? AggregatedEvent;
                                         public void Subscribe(EventPublisher publisher) { publisher.MessageEvent += HandleMessage; publisher.SimpleEvent += HandleSimple; }
                                         private void HandleMessage(string message) { lock (_lock) { _eventCount++; AggregatedEvent?.Invoke(this, new AggregatedEventArgs($"Message: {message}", _eventCount)); } }
                                         private void HandleSimple() { lock (_lock) { _eventCount++; AggregatedEvent?.Invoke(this, new AggregatedEventArgs("Simple event", _eventCount)); } }
                                     }

                                     public class AggregatedEventArgs : EventArgs
                                     {
                                         public AggregatedEventArgs(string description, int eventCount) { Description = description; EventCount = eventCount; }
                                         public string Description { get; }
                                         public int EventCount { get; }
                                     }

                                     public class GenericEventHandler<T>
                                     {
                                         public event EventHandler<GenericEventArgs<T>>? DataEvent;
                                         public void TriggerEvent(T data) { DataEvent?.Invoke(this, new GenericEventArgs<T>(data)); }
                                     }

                                     public class GenericEventArgs<T> : EventArgs
                                     {
                                         public GenericEventArgs(T data) { Data = data; }
                                         public T Data { get; }
                                     }

                                     public class ConditionalEventHandler
                                     {
                                         private bool _isSubscribed;
                                         public void SubscribeConditionally(EventPublisher publisher, bool shouldSubscribe)
                                         {
                                             if (shouldSubscribe && !_isSubscribed) { publisher.MessageEvent += HandleMessage; _isSubscribed = true; }
                                             else if (!shouldSubscribe && _isSubscribed) { publisher.MessageEvent -= HandleMessage; _isSubscribed = false; }
                                         }
                                         private void HandleMessage(string message) { Console.WriteLine($"Conditionally handled: {message}"); }
                                     }
                                     """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[]
        {
            "EventPublisher", "CustomEventArgs", "DataChangedEventArgs", "EventSubscriber", "EventLogger",
            "EventCounter", "EventInvoker", "EventSink", "EventChain", "EventAggregator", "AggregatedEventArgs",
            "GenericEventHandler", "GenericEventArgs", "ConditionalEventHandler"
        };

        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void EventSubscriptions_AreDetectedAsHandles()
    {
        var expected = new[]
        {
            "EventSubscriber.HandleSimpleEvent -> EventPublisher.SimpleEvent",
            "EventSubscriber.HandleMessageEvent -> EventPublisher.MessageEvent",
            "EventSubscriber.HandleCustomEvent -> EventPublisher.CustomEvent",
            "EventSubscriber.HandleDataChanged -> EventPublisher.DataChanged",
            "EventSubscriber.StaticMessageHandler -> EventPublisher.MessageEvent",
            "EventLogger.LogMessage -> EventPublisher.MessageEvent",
            "EventLogger.LogCustomEvent -> EventPublisher.CustomEvent",
            "EventCounter.CountEvent -> EventPublisher.SimpleEvent",
            "EventCounter.CountMessageEvent -> EventPublisher.MessageEvent",
            "EventSink.HandleEvent -> IEventProvider.MyEvent",
            "EventChain.TriggerSecondEvent -> EventChain.FirstEvent",
            "EventChain.TriggerThirdEvent -> EventChain.SecondEvent",
            "EventAggregator.HandleMessage -> EventPublisher.MessageEvent",
            "EventAggregator.HandleSimple -> EventPublisher.SimpleEvent",
            "ConditionalEventHandler.HandleMessage -> EventPublisher.MessageEvent"
        };

        Assert.That(RelsOf(RelationshipType.Handles), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_AreDetected()
    {
        var expected = new[]
        {
            "CustomEventArgs..ctor -> CustomEventArgs.Data",
            "DataChangedEventArgs..ctor -> DataChangedEventArgs.OldValue",
            "DataChangedEventArgs..ctor -> DataChangedEventArgs.NewValue",
            "EventSubscriber..ctor -> EventSubscriber.SubscribeToEvents",
            "EventSubscriber.HandleCustomEvent -> CustomEventArgs.Data",
            "EventSubscriber.HandleDataChanged -> DataChangedEventArgs.NewValue",
            "EventSubscriber.HandleDataChanged -> DataChangedEventArgs.OldValue",
            "EventLogger.LogCustomEvent -> CustomEventArgs.Data",
            "EventInvoker.TriggerAllInvocations -> EventInvoker.InvokeMethod1",
            "EventInvoker.TriggerAllInvocations -> EventInvoker.InvokeMethod2",
            "EventInvoker.TriggerAllInvocations -> EventInvoker.InvokeMethod3",
            "EventInvoker.TriggerAllInvocations -> EventInvoker.InvokeMethod4",
            "EventInvoker.TriggerAllInvocations -> EventInvoker.InvokeMethod5",
            "AggregatedEventArgs..ctor -> AggregatedEventArgs.Description",
            "AggregatedEventArgs..ctor -> AggregatedEventArgs.EventCount",
            "EventAggregator.HandleMessage -> AggregatedEventArgs..ctor",
            "EventAggregator.HandleSimple -> AggregatedEventArgs..ctor",
            "EventPublisher.TriggerCustomEvent -> CustomEventArgs..ctor",
            "EventPublisher.TriggerDataChanged -> DataChangedEventArgs..ctor",
            "GenericEventArgs..ctor -> GenericEventArgs.Data",
            "GenericEventHandler.TriggerEvent -> GenericEventArgs..ctor"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
