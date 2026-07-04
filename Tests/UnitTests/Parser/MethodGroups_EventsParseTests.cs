using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Method groups in event handling: modern "+=" registration, the old "new EventHandler(...)" style,
///     and method-group subscription through an interface. Registrations become "Handles" edges, the
///     method-group references become IsMethodGroup "Uses". Migrated from the former Core.MethodGroups
///     approval fixture (EventMethodGroups.cs).
/// </summary>
[TestFixture]
public class MethodGroups_EventsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                     using System;

                                     namespace Demo;

                                     public class EventMethodGroups
                                     {
                                         public event Action<string>? StringEvent;
                                         public event Func<int, bool>? ValidationEvent;
                                         public event EventHandler? LegacyEvent;

                                         public void SetupEventHandlers()
                                         {
                                             StringEvent += HandleStringEvent;
                                             StringEvent += StaticStringHandler;
                                             StringEvent -= HandleStringEvent;
                                             ValidationEvent += ValidatePositive;
                                             ValidationEvent += ValidateEven;
                                         }

                                         public void SetupLegacyHandler() { LegacyEvent += new EventHandler(OnLegacy); }

                                         private void OnLegacy(object? sender, EventArgs e) { Console.WriteLine("Legacy event handled"); }

                                         public void TestEventInvocation()
                                         {
                                             StringEvent?.Invoke("test message");
                                             var isValid = ValidationEvent?.Invoke(42) ?? false;
                                         }

                                         private void HandleStringEvent(string message) { Console.WriteLine($"Event handled: {message}"); }
                                         private static void StaticStringHandler(string message) { Console.WriteLine($"Static event handler: {message}"); }
                                         private bool ValidatePositive(int number) { return number > 0; }
                                         private bool ValidateEven(int number) { return number % 2 == 0; }
                                     }

                                     public interface IEventProvider { event Action<string> MessageReceived; }

                                     public class EventProvider : IEventProvider
                                     {
                                         public event Action<string>? MessageReceived;
                                         public void TriggerEvent(string message) { MessageReceived?.Invoke(message); }
                                     }

                                     public class EventConsumer
                                     {
                                         private readonly IEventProvider _provider;
                                         public EventConsumer(IEventProvider provider) { _provider = provider; _provider.MessageReceived += HandleMessage; }
                                         private void HandleMessage(string message) { Console.WriteLine($"Consumed: {message}"); }
                                     }
                                     """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[] { "EventMethodGroups", "EventProvider", "EventConsumer" };
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void Methods_AreDetected()
    {
        var expected = new[]
        {
            "EventMethodGroups.SetupEventHandlers", "EventMethodGroups.TestEventInvocation",
            "EventMethodGroups.HandleStringEvent", "EventMethodGroups.StaticStringHandler",
            "EventMethodGroups.ValidateEven", "EventMethodGroups.ValidatePositive",
            "EventMethodGroups.SetupLegacyHandler", "EventMethodGroups.OnLegacy",
            "EventConsumer..ctor", "EventConsumer.HandleMessage",
            "EventProvider.TriggerEvent"
        };

        Assert.That(PathsOf(CodeElementType.Method), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodGroupUsages_AreDetected()
    {
        var expected = new[]
        {
            "EventMethodGroups.SetupEventHandlers -> EventMethodGroups.HandleStringEvent",
            "EventMethodGroups.SetupEventHandlers -> EventMethodGroups.StaticStringHandler",
            "EventMethodGroups.SetupEventHandlers -> EventMethodGroups.ValidatePositive",
            "EventMethodGroups.SetupEventHandlers -> EventMethodGroups.ValidateEven",
            "EventMethodGroups.SetupLegacyHandler -> EventMethodGroups.OnLegacy",
            "EventConsumer..ctor -> EventConsumer.HandleMessage"
        };

        Assert.That(MethodGroupUsages(), Is.EquivalentTo(expected));
    }

    [Test]
    public void EventSubscriptions_AreDetectedAsHandles()
    {
        var expected = new[]
        {
            "EventMethodGroups.HandleStringEvent -> EventMethodGroups.StringEvent",
            "EventMethodGroups.StaticStringHandler -> EventMethodGroups.StringEvent",
            "EventMethodGroups.ValidatePositive -> EventMethodGroups.ValidationEvent",
            "EventMethodGroups.ValidateEven -> EventMethodGroups.ValidationEvent",
            "EventMethodGroups.OnLegacy -> EventMethodGroups.LegacyEvent",
            "EventConsumer.HandleMessage -> IEventProvider.MessageReceived"
        };

        Assert.That(RelsOf(RelationshipType.Handles), Is.EquivalentTo(expected));
    }
}
