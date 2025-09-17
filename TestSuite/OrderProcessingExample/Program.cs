namespace OrderProcessingExample;

public interface INotificationService
{
    void SendNotification(string message);
}

public class EmailNotificationService : INotificationService
{
    public void SendNotification(string message)
    {
        Console.WriteLine($"📧 Email sent: {message}");
    }
}

public class OrderEventArgs : EventArgs
{

    public OrderEventArgs(string orderId, decimal amount)
    {
        OrderId = orderId;
        Amount = amount;
    }

    public string OrderId { get; set; }
    public decimal Amount { get; set; }
}

public class OrderProcessor : IOrderProcessor, IOrderProcessorEvt
{

    private readonly INotificationService _notificationService;

    public OrderProcessor(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public void ProcessMultipleOrders()
    {
        OnOrderCompleted(new OrderEventArgs("id", 10));
    }

    public event EventHandler<OrderEventArgs> OrderCompleted;


    public virtual void OnOrderCompleted(OrderEventArgs e)
    {
        OrderCompleted?.Invoke(this, e);
    }
}

public interface IOrderProcessorEvt
{
    event EventHandler<OrderEventArgs> OrderCompleted;
}

public interface IOrderProcessor
{
    void ProcessMultipleOrders();
}

public class OrderEventHandler
{
    private readonly INotificationService _notificationService;

    public OrderEventHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public void HandleOrderCompleted(object sender, OrderEventArgs e)
    {
        _notificationService.SendNotification($"Congratulations! Your order {e.OrderId} for ${e.Amount} has been completed.");

        LogOrderCompletion(e.OrderId, e.Amount);
    }

    private void LogOrderCompletion(string orderId, decimal amount)
    {
    }
}

// Main program
public class Program
{
    public static void Main()
    {
        INotificationService emailService = new EmailNotificationService();
        IOrderProcessor orderProcessor = new OrderProcessor(emailService);
        var eventHandler = new OrderEventHandler(emailService);
        ((IOrderProcessorEvt)orderProcessor).OrderCompleted += eventHandler.HandleOrderCompleted;

        orderProcessor.ProcessMultipleOrders();
    }
}