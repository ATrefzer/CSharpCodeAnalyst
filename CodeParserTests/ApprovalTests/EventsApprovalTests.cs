using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class EventsApprovalTests : ProjectTestBase
{
    private CodeGraph GetTestGraph()
    {
        return GetAssemblyGraph("Core.Events");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestGraph()).ToList();

        var expected = new[]
        {
            "Core.Events.Core.Events.EventPublisher",
            "Core.Events.Core.Events.CustomEventArgs",
            "Core.Events.Core.Events.DataChangedEventArgs",
            "Core.Events.Core.Events.EventSubscriber",
            "Core.Events.Core.Events.EventLogger",
            "Core.Events.Core.Events.EventCounter",
            "Core.Events.Core.Events.EventInvoker",
            "Core.Events.Core.Events.EventSink",
            "Core.Events.Core.Events.EventChain",
            "Core.Events.Core.Events.EventAggregator",
            "Core.Events.Core.Events.AggregatedEventArgs",
            "Core.Events.Core.Events.ConditionalEventHandler",
            "Core.Events.Core.Events.DataListener",
            "Core.Events.Core.Events.DataProcessor",
            "Core.Events.Core.Events.EmailNotificationService",
            "Core.Events.Core.Events.ErrorEventArgs",
            "Core.Events.Core.Events.GenericEventArgs",
            "Core.Events.Core.Events.GenericEventHandler",
            "Core.Events.Core.Events.NotificationMonitor",
            "Core.Events.Core.Events.ProcessedDataEventArgs",
            "Core.Events.Core.Events.SmsNotificationService"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    [Test]
    public void EventSubscriptions_ShouldBeDetected()
    {
        var eventSubscriptions = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Handles);

        var expected = new[]
        {
            "Core.Events.Core.Events.EventSubscriber.HandleSimpleEvent -> Core.Events.Core.Events.EventPublisher.SimpleEvent",
            "Core.Events.Core.Events.EventSubscriber.HandleMessageEvent -> Core.Events.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.Core.Events.EventSubscriber.HandleCustomEvent -> Core.Events.Core.Events.EventPublisher.CustomEvent",
            "Core.Events.Core.Events.EventSubscriber.HandleDataChanged -> Core.Events.Core.Events.EventPublisher.DataChanged",
            "Core.Events.Core.Events.EventSubscriber.StaticMessageHandler -> Core.Events.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.Core.Events.EventLogger.LogMessage -> Core.Events.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.Core.Events.EventLogger.LogCustomEvent -> Core.Events.Core.Events.EventPublisher.CustomEvent",
            "Core.Events.Core.Events.EventCounter.CountEvent -> Core.Events.Core.Events.EventPublisher.SimpleEvent",
            "Core.Events.Core.Events.EventCounter.CountMessageEvent -> Core.Events.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.Core.Events.EventSink.HandleEvent -> Core.Events.Core.Events.IEventProvider.MyEvent",
            "Core.Events.Core.Events.ConditionalEventHandler.HandleMessage -> Core.Events.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.Core.Events.DataListener.HandleConnectionLost -> Core.Events.Core.Events.IEventSource.ConnectionLost",
            "Core.Events.Core.Events.DataListener.HandleDataProcessed -> Core.Events.Core.Events.IEventProcessor.DataProcessed",
            "Core.Events.Core.Events.DataListener.HandleDataReceived -> Core.Events.Core.Events.IEventSource.DataReceived",
            "Core.Events.Core.Events.EventAggregator.HandleMessage -> Core.Events.Core.Events.EventPublisher.MessageEvent",
            "Core.Events.Core.Events.EventAggregator.HandleSimple -> Core.Events.Core.Events.EventPublisher.SimpleEvent",
            "Core.Events.Core.Events.EventChain.TriggerSecondEvent -> Core.Events.Core.Events.EventChain.FirstEvent",
            "Core.Events.Core.Events.EventChain.TriggerThirdEvent -> Core.Events.Core.Events.EventChain.SecondEvent",
            "Core.Events.Core.Events.NotificationMonitor.HandleError -> Core.Events.Core.Events.INotificationService.ErrorOccurred",
            "Core.Events.Core.Events.NotificationMonitor.HandleNotificationSent -> Core.Events.Core.Events.INotificationService.NotificationSent"
        };

        CollectionAssert.AreEquivalent(expected, eventSubscriptions);
    }



    [Test]
    public void MethodCalls_ShouldBeDetected()
    {
        var callRelationships = GetRelationshipsOfType(GetTestGraph(), RelationshipType.Calls);


        var expected = new[]
        {
            "Core.Events.Core.Events.CustomEventArgs..ctor -> Core.Events.Core.Events.CustomEventArgs.Data",
            "Core.Events.Core.Events.DataChangedEventArgs..ctor -> Core.Events.Core.Events.DataChangedEventArgs.OldValue",
            "Core.Events.Core.Events.DataChangedEventArgs..ctor -> Core.Events.Core.Events.DataChangedEventArgs.NewValue",
            "Core.Events.Core.Events.EventSubscriber..ctor -> Core.Events.Core.Events.EventSubscriber.SubscribeToEvents",
            "Core.Events.Core.Events.EventSubscriber.HandleCustomEvent -> Core.Events.Core.Events.CustomEventArgs.Data",
            "Core.Events.Core.Events.EventLogger.LogCustomEvent -> Core.Events.Core.Events.CustomEventArgs.Data",
            "Core.Events.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.Core.Events.EventInvoker.InvokeMethod1",
            "Core.Events.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.Core.Events.EventInvoker.InvokeMethod2",
            "Core.Events.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.Core.Events.EventInvoker.InvokeMethod3",
            "Core.Events.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.Core.Events.EventInvoker.InvokeMethod4",
            "Core.Events.Core.Events.AggregatedEventArgs..ctor -> Core.Events.Core.Events.AggregatedEventArgs.Description",
            "Core.Events.Core.Events.AggregatedEventArgs..ctor -> Core.Events.Core.Events.AggregatedEventArgs.EventCount",
            "Core.Events.Core.Events.DataListener.HandleDataProcessed -> Core.Events.Core.Events.ProcessedDataEventArgs.OriginalData",
            "Core.Events.Core.Events.DataListener.HandleDataProcessed -> Core.Events.Core.Events.ProcessedDataEventArgs.ProcessedData",
            "Core.Events.Core.Events.DataProcessor.ReceiveData -> Core.Events.Core.Events.DataProcessor.ProcessData",
            "Core.Events.Core.Events.ErrorEventArgs..ctor -> Core.Events.Core.Events.ErrorEventArgs.Exception",
            "Core.Events.Core.Events.EventInvoker.TriggerAllInvocations -> Core.Events.Core.Events.EventInvoker.InvokeMethod5",
            "Core.Events.Core.Events.GenericEventArgs..ctor -> Core.Events.Core.Events.GenericEventArgs.Data",
            "Core.Events.Core.Events.NotificationMonitor.HandleError -> Core.Events.Core.Events.ErrorEventArgs.Exception",
            "Core.Events.Core.Events.ProcessedDataEventArgs..ctor -> Core.Events.Core.Events.ProcessedDataEventArgs.OriginalData",
            "Core.Events.Core.Events.ProcessedDataEventArgs..ctor -> Core.Events.Core.Events.ProcessedDataEventArgs.ProcessedData",

            "Core.Events.Core.Events.DataProcessor.ReceiveData -> Core.Events.Core.Events.ProcessedDataEventArgs..ctor",
            "Core.Events.Core.Events.EmailNotificationService.SendNotification -> Core.Events.Core.Events.ErrorEventArgs..ctor",
            "Core.Events.Core.Events.EmailNotificationService.TriggerError -> Core.Events.Core.Events.ErrorEventArgs..ctor",
            "Core.Events.Core.Events.EventAggregator.HandleMessage -> Core.Events.Core.Events.AggregatedEventArgs..ctor",
            "Core.Events.Core.Events.EventAggregator.HandleSimple -> Core.Events.Core.Events.AggregatedEventArgs..ctor",
            "Core.Events.Core.Events.EventPublisher.TriggerCustomEvent -> Core.Events.Core.Events.CustomEventArgs..ctor",
            "Core.Events.Core.Events.EventPublisher.TriggerDataChanged -> Core.Events.Core.Events.DataChangedEventArgs..ctor",
            "Core.Events.Core.Events.GenericEventHandler.TriggerEvent -> Core.Events.Core.Events.GenericEventArgs..ctor",
            "Core.Events.Core.Events.SmsNotificationService.SendNotification -> Core.Events.Core.Events.ErrorEventArgs..ctor",


            "Core.Events.Core.Events.EventSubscriber.HandleDataChanged -> Core.Events.Core.Events.DataChangedEventArgs.NewValue",
            "Core.Events.Core.Events.EventSubscriber.HandleDataChanged -> Core.Events.Core.Events.DataChangedEventArgs.OldValue"
        };

        CollectionAssert.AreEquivalent(expected, callRelationships);
    }
}