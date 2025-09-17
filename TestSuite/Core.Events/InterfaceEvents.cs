using System;

namespace Core.Events;

// Test interface events
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
        try
        {
            // Simulate email sending
            Console.WriteLine($"Sending email: {message}");
            NotificationSent?.Invoke(message);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex));
        }
    }

    public void TriggerError()
    {
        ErrorOccurred?.Invoke(this, new ErrorEventArgs(new InvalidOperationException("Test error")));
    }
}

public class SmsNotificationService : INotificationService
{
    public event Action<string>? NotificationSent;
    public event EventHandler<ErrorEventArgs>? ErrorOccurred;

    public void SendNotification(string message)
    {
        try
        {
            // Simulate SMS sending
            Console.WriteLine($"Sending SMS: {message}");
            NotificationSent?.Invoke(message);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex));
        }
    }
}

public class ErrorEventArgs : EventArgs
{

    public ErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }

    public Exception Exception { get; }
}

// Event consumer using interface
public class NotificationMonitor
{
    public NotificationMonitor(INotificationService service)
    {
        service.NotificationSent += HandleNotificationSent;
        service.ErrorOccurred += HandleError;
    }

    private void HandleNotificationSent(string message)
    {
        Console.WriteLine($"[MONITOR] Notification sent: {message}");
    }

    private void HandleError(object? sender, ErrorEventArgs e)
    {
        Console.WriteLine($"[MONITOR] Error from {sender}: {e.Exception.Message}");
    }
}

// Multiple interface event implementations
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

    public ProcessedDataEventArgs(string originalData, string processedData)
    {
        OriginalData = originalData;
        ProcessedData = processedData;
    }

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

    public void SimulateConnectionLoss()
    {
        ConnectionLost?.Invoke(this, EventArgs.Empty);
    }

    private string ProcessData(string data)
    {
        return data.ToUpperInvariant();
    }
}

public class DataListener
{
    public DataListener(IEventProcessor processor)
    {
        processor.DataReceived += HandleDataReceived;
        processor.DataProcessed += HandleDataProcessed;
        processor.ConnectionLost += HandleConnectionLost;
    }

    private void HandleDataReceived(object? sender, string data)
    {
        Console.WriteLine($"[LISTENER] Data received: {data}");
    }

    private void HandleDataProcessed(object? sender, ProcessedDataEventArgs e)
    {
        Console.WriteLine($"[LISTENER] Data processed: {e.OriginalData} -> {e.ProcessedData}");
    }

    private void HandleConnectionLost(object? sender, EventArgs e)
    {
        Console.WriteLine("[LISTENER] Connection lost!");
    }
}