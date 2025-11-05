using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class EventsApprovalTests : ApprovalTestBase
{
    private CodeGraph.Graph.CodeGraph GetTestGraph()
    {
        return GetTestGraph("Core.Events");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Core.Events.global.Core.Events.EventPublisher",
            "Core.Events.global.Core.Events.CustomEventArgs",
            "Core.Events.global.Core.Events.DataChangedEventArgs",
            "Core.Events.global.Core.Events.EventSubscriber",
            "Core.Events.global.Core.Events.EventLogger",
            "Core.Events.global.Core.Events.EventCounter",
            "Core.Events.global.Core.Events.EventInvoker",
            "Core.Events.global.Core.Events.EventSink",
            "Core.Events.global.Core.Events.EventChain",
            "Core.Events.global.Core.Events.EventAggregator",
            "Core.Events.global.Core.Events.AggregatedEventArgs",
            "Core.Events.global.Core.Events.ConditionalEventHandler",
            "Core.Events.global.Core.Events.DataListener",
            "Core.Events.global.Core.Events.DataProcessor",
            "Core.Events.global.Core.Events.EmailNotificationService",
            "Core.Events.global.Core.Events.ErrorEventArgs",
            "Core.Events.global.Core.Events.GenericEventArgs",
            "Core.Events.global.Core.Events.GenericEventHandler",
            "Core.Events.global.Core.Events.NotificationMonitor",
            "Core.Events.global.Core.Events.ProcessedDataEventArgs",
            "Core.Events.global.Core.Events.SmsNotificationService"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    [Test]
    public void EventSubscriptions_ShouldBeDetected()
    {
        var eventSubscriptions = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Handles);

        var expected = new[]
        {
            "Core.Events.global.Core.Events.EventSubscriber.HandleSimpleEvent -> Core.Events.global.Core.Events.EventPublisher.SimpleEvent",
            "Core.Events.global.Core.Events.EventSubscriber.HandleMessageEvent -> Core.Events.global.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.global.Core.Events.EventSubscriber.HandleCustomEvent -> Core.Events.global.Core.Events.EventPublisher.CustomEvent",
            "Core.Events.global.Core.Events.EventSubscriber.HandleDataChanged -> Core.Events.global.Core.Events.EventPublisher.DataChanged",
            "Core.Events.global.Core.Events.EventSubscriber.StaticMessageHandler -> Core.Events.global.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.global.Core.Events.EventLogger.LogMessage -> Core.Events.global.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.global.Core.Events.EventLogger.LogCustomEvent -> Core.Events.global.Core.Events.EventPublisher.CustomEvent",
            "Core.Events.global.Core.Events.EventCounter.CountEvent -> Core.Events.global.Core.Events.EventPublisher.SimpleEvent",
            "Core.Events.global.Core.Events.EventCounter.CountMessageEvent -> Core.Events.global.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.global.Core.Events.EventSink.HandleEvent -> Core.Events.global.Core.Events.IEventProvider.MyEvent",
            "Core.Events.global.Core.Events.ConditionalEventHandler.HandleMessage -> Core.Events.global.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.global.Core.Events.DataListener.HandleConnectionLost -> Core.Events.global.Core.Events.IEventSource.ConnectionLost",
            "Core.Events.global.Core.Events.DataListener.HandleDataProcessed -> Core.Events.global.Core.Events.IEventProcessor.DataProcessed",
            "Core.Events.global.Core.Events.DataListener.HandleDataReceived -> Core.Events.global.Core.Events.IEventSource.DataReceived",
            "Core.Events.global.Core.Events.EventAggregator.HandleMessage -> Core.Events.global.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.global.Core.Events.EventAggregator.HandleSimple -> Core.Events.global.Core.Events.EventPublisher.SimpleEvent",
            "Core.Events.global.Core.Events.EventChain.TriggerSecondEvent -> Core.Events.global.Core.Events.EventChain.FirstEvent",
            "Core.Events.global.Core.Events.EventChain.TriggerThirdEvent -> Core.Events.global.Core.Events.EventChain.SecondEvent",
            "Core.Events.global.Core.Events.NotificationMonitor.HandleError -> Core.Events.global.Core.Events.INotificationService.ErrorOccurred",
            "Core.Events.global.Core.Events.NotificationMonitor.HandleNotificationSent -> Core.Events.global.Core.Events.INotificationService.NotificationSent"
        };

        CollectionAssert.AreEquivalent(expected, eventSubscriptions);
    }



    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var callRelationships = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);


        var expected = new[]
        {
            "Core.Events.global.Core.Events.CustomEventArgs..ctor -> Core.Events.global.Core.Events.CustomEventArgs.Data",
            "Core.Events.global.Core.Events.DataChangedEventArgs..ctor -> Core.Events.global.Core.Events.DataChangedEventArgs.OldValue",
            "Core.Events.global.Core.Events.DataChangedEventArgs..ctor -> Core.Events.global.Core.Events.DataChangedEventArgs.NewValue",
            "Core.Events.global.Core.Events.EventSubscriber..ctor -> Core.Events.global.Core.Events.EventSubscriber.SubscribeToEvents",
            "Core.Events.global.Core.Events.EventSubscriber.HandleCustomEvent -> Core.Events.global.Core.Events.CustomEventArgs.Data",
            "Core.Events.global.Core.Events.EventLogger.LogCustomEvent -> Core.Events.global.Core.Events.CustomEventArgs.Data",
            "Core.Events.global.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.global.Core.Events.EventInvoker.InvokeMethod1",
            "Core.Events.global.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.global.Core.Events.EventInvoker.InvokeMethod2",
            "Core.Events.global.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.global.Core.Events.EventInvoker.InvokeMethod3",
            "Core.Events.global.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.global.Core.Events.EventInvoker.InvokeMethod4",
            "Core.Events.global.Core.Events.AggregatedEventArgs..ctor -> Core.Events.global.Core.Events.AggregatedEventArgs.Description",
            "Core.Events.global.Core.Events.AggregatedEventArgs..ctor -> Core.Events.global.Core.Events.AggregatedEventArgs.EventCount",
            "Core.Events.global.Core.Events.DataListener.HandleDataProcessed -> Core.Events.global.Core.Events.ProcessedDataEventArgs.OriginalData",
            "Core.Events.global.Core.Events.DataListener.HandleDataProcessed -> Core.Events.global.Core.Events.ProcessedDataEventArgs.ProcessedData",
            "Core.Events.global.Core.Events.DataProcessor.ReceiveData -> Core.Events.global.Core.Events.DataProcessor.ProcessData",
            "Core.Events.global.Core.Events.ErrorEventArgs..ctor -> Core.Events.global.Core.Events.ErrorEventArgs.Exception",
            "Core.Events.global.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.global.Core.Events.EventInvoker.InvokeMethod5",
            "Core.Events.global.Core.Events.GenericEventArgs..ctor -> Core.Events.global.Core.Events.GenericEventArgs.Data",
            "Core.Events.global.Core.Events.NotificationMonitor.HandleError -> Core.Events.global.Core.Events.ErrorEventArgs.Exception",
            "Core.Events.global.Core.Events.ProcessedDataEventArgs..ctor -> Core.Events.global.Core.Events.ProcessedDataEventArgs.OriginalData",
            "Core.Events.global.Core.Events.ProcessedDataEventArgs..ctor -> Core.Events.global.Core.Events.ProcessedDataEventArgs.ProcessedData",

            "Core.Events.global.Core.Events.DataProcessor.ReceiveData -> Core.Events.global.Core.Events.ProcessedDataEventArgs..ctor",
            "Core.Events.global.Core.Events.EmailNotificationService.SendNotification -> Core.Events.global.Core.Events.ErrorEventArgs..ctor",
            "Core.Events.global.Core.Events.EmailNotificationService.TriggerError -> Core.Events.global.Core.Events.ErrorEventArgs..ctor",
            "Core.Events.global.Core.Events.EventAggregator.HandleMessage -> Core.Events.global.Core.Events.AggregatedEventArgs..ctor",
            "Core.Events.global.Core.Events.EventAggregator.HandleSimple -> Core.Events.global.Core.Events.AggregatedEventArgs..ctor",
            "Core.Events.global.Core.Events.EventPublisher.TriggerCustomEvent -> Core.Events.global.Core.Events.CustomEventArgs..ctor",
            "Core.Events.global.Core.Events.EventPublisher.TriggerDataChanged -> Core.Events.global.Core.Events.DataChangedEventArgs..ctor",
            "Core.Events.global.Core.Events.GenericEventHandler.TriggerEvent -> Core.Events.global.Core.Events.GenericEventArgs..ctor",
            "Core.Events.global.Core.Events.SmsNotificationService.SendNotification -> Core.Events.global.Core.Events.ErrorEventArgs..ctor",


            "Core.Events.global.Core.Events.EventSubscriber.HandleDataChanged -> Core.Events.global.Core.Events.DataChangedEventArgs.NewValue",
            "Core.Events.global.Core.Events.EventSubscriber.HandleDataChanged -> Core.Events.global.Core.Events.DataChangedEventArgs.OldValue"
        };

        CollectionAssert.AreEquivalent(expected, callRelationships);
    }
}