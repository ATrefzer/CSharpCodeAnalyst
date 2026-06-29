using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Interface events: services that declare events through an interface, interface event inheritance
///     (IEventProcessor : IEventSource), and consumers that subscribe to interface-declared events
///     (Handles). Migrated from the former Core.Events approval fixture (InterfaceEvents.cs).
/// </summary>
[TestFixture]
public class Events_InterfaceParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                     using System;

                                     namespace Demo;

                                     public interface INotificationService
                                     {
                                         event Action<string>? NotificationSent;
                                         event EventHandler<ErrorEventArgs>? ErrorOccurred;
                                         void SendNotification(string message);
                                     }

                                     public class EmailNotificationService : INotificationService
                                     {
                                         public event Action<string>? NotificationSent;
                                         public event EventHandler<ErrorEventArgs>? ErrorOccurred;
                                         public void SendNotification(string message)
                                         {
                                             try { Console.WriteLine($"Sending email: {message}"); NotificationSent?.Invoke(message); }
                                             catch (Exception ex) { ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex)); }
                                         }
                                         public void TriggerError() { ErrorOccurred?.Invoke(this, new ErrorEventArgs(new InvalidOperationException("Test error"))); }
                                     }

                                     public class SmsNotificationService : INotificationService
                                     {
                                         public event Action<string>? NotificationSent;
                                         public event EventHandler<ErrorEventArgs>? ErrorOccurred;
                                         public void SendNotification(string message)
                                         {
                                             try { Console.WriteLine($"Sending SMS: {message}"); NotificationSent?.Invoke(message); }
                                             catch (Exception ex) { ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex)); }
                                         }
                                     }

                                     public class ErrorEventArgs : EventArgs
                                     {
                                         public ErrorEventArgs(Exception exception) { Exception = exception; }
                                         public Exception Exception { get; }
                                     }

                                     public class NotificationMonitor
                                     {
                                         public NotificationMonitor(INotificationService service) { service.NotificationSent += HandleNotificationSent; service.ErrorOccurred += HandleError; }
                                         private void HandleNotificationSent(string message) { Console.WriteLine($"[MONITOR] Notification sent: {message}"); }
                                         private void HandleError(object? sender, ErrorEventArgs e) { Console.WriteLine($"[MONITOR] Error from {sender}: {e.Exception.Message}"); }
                                     }

                                     public interface IEventSource
                                     {
                                         event EventHandler<string>? DataReceived;
                                         event EventHandler? ConnectionLost;
                                     }

                                     public interface IEventProcessor : IEventSource
                                     {
                                         event EventHandler<ProcessedDataEventArgs>? DataProcessed;
                                     }

                                     public class ProcessedDataEventArgs : EventArgs
                                     {
                                         public ProcessedDataEventArgs(string originalData, string processedData) { OriginalData = originalData; ProcessedData = processedData; }
                                         public string OriginalData { get; }
                                         public string ProcessedData { get; }
                                     }

                                     public class DataProcessor : IEventProcessor
                                     {
                                         public event EventHandler<string>? DataReceived;
                                         public event EventHandler? ConnectionLost;
                                         public event EventHandler<ProcessedDataEventArgs>? DataProcessed;
                                         public void ReceiveData(string data)
                                         {
                                             DataReceived?.Invoke(this, data);
                                             var processedData = ProcessData(data);
                                             DataProcessed?.Invoke(this, new ProcessedDataEventArgs(data, processedData));
                                         }
                                         public void SimulateConnectionLoss() { ConnectionLost?.Invoke(this, EventArgs.Empty); }
                                         private string ProcessData(string data) { return data.ToUpperInvariant(); }
                                     }

                                     public class DataListener
                                     {
                                         public DataListener(IEventProcessor processor)
                                         {
                                             processor.DataReceived += HandleDataReceived;
                                             processor.DataProcessed += HandleDataProcessed;
                                             processor.ConnectionLost += HandleConnectionLost;
                                         }
                                         private void HandleDataReceived(object? sender, string data) { Console.WriteLine($"[LISTENER] Data received: {data}"); }
                                         private void HandleDataProcessed(object? sender, ProcessedDataEventArgs e) { Console.WriteLine($"[LISTENER] Data processed: {e.OriginalData} -> {e.ProcessedData}"); }
                                         private void HandleConnectionLost(object? sender, EventArgs e) { Console.WriteLine("[LISTENER] Connection lost!"); }
                                     }
                                     """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[]
        {
            "EmailNotificationService", "SmsNotificationService", "ErrorEventArgs", "NotificationMonitor",
            "ProcessedDataEventArgs", "DataProcessor", "DataListener"
        };

        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void EventSubscriptions_AreDetectedAsHandles()
    {
        var expected = new[]
        {
            "NotificationMonitor.HandleNotificationSent -> INotificationService.NotificationSent",
            "NotificationMonitor.HandleError -> INotificationService.ErrorOccurred",
            "DataListener.HandleDataReceived -> IEventSource.DataReceived",
            "DataListener.HandleDataProcessed -> IEventProcessor.DataProcessed",
            "DataListener.HandleConnectionLost -> IEventSource.ConnectionLost"
        };

        Assert.That(RelsOf(RelationshipType.Handles), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_AreDetected()
    {
        var expected = new[]
        {
            "EmailNotificationService.SendNotification -> ErrorEventArgs..ctor",
            "EmailNotificationService.TriggerError -> ErrorEventArgs..ctor",
            "SmsNotificationService.SendNotification -> ErrorEventArgs..ctor",
            "ErrorEventArgs..ctor -> ErrorEventArgs.Exception",
            "NotificationMonitor.HandleError -> ErrorEventArgs.Exception",
            "ProcessedDataEventArgs..ctor -> ProcessedDataEventArgs.OriginalData",
            "ProcessedDataEventArgs..ctor -> ProcessedDataEventArgs.ProcessedData",
            "DataProcessor.ReceiveData -> DataProcessor.ProcessData",
            "DataProcessor.ReceiveData -> ProcessedDataEventArgs..ctor",
            "DataListener.HandleDataProcessed -> ProcessedDataEventArgs.OriginalData",
            "DataListener.HandleDataProcessed -> ProcessedDataEventArgs.ProcessedData"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
