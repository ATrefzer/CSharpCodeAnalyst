using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A small end-to-end order-processing example: interface implementation (incl. an event declared on
///     an interface), event subscription through an interface cast (Handles), event invocation (Invokes)
///     and the surrounding object creations and calls. Migrated from the former OrderProcessingExample
///     project (Program.cs).
/// </summary>
[TestFixture]
public class OrderProcessingParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                     using System;

                                     namespace Demo;

                                     public interface INotificationService
                                     {
                                         void SendNotification(string message);
                                     }

                                     public class EmailNotificationService : INotificationService
                                     {
                                         public void SendNotification(string message)
                                         {
                                             Console.WriteLine($"Email sent: {message}");
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
                                             _notificationService.SendNotification($"Order {e.OrderId} for ${e.Amount} completed.");
                                             LogOrderCompletion(e.OrderId, e.Amount);
                                         }

                                         private void LogOrderCompletion(string orderId, decimal amount)
                                         {
                                         }
                                     }

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
                                     """;

    [Test]
    public void ClassesAndInterfaces_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(new[]
        {
            "EmailNotificationService", "OrderEventArgs", "OrderProcessor", "OrderEventHandler", "Program"
        }));
        Assert.That(PathsOf(CodeElementType.Interface), Is.EquivalentTo(new[]
        {
            "INotificationService", "IOrderProcessorEvt", "IOrderProcessor"
        }));
    }

    [Test]
    public void InterfaceImplementations_AreDetected()
    {
        var expected = new[]
        {
            "EmailNotificationService -> INotificationService",
            "EmailNotificationService.SendNotification -> INotificationService.SendNotification",
            "OrderProcessor -> IOrderProcessor",
            "OrderProcessor -> IOrderProcessorEvt",
            "OrderProcessor.ProcessMultipleOrders -> IOrderProcessor.ProcessMultipleOrders",

            // The event member implements the interface event.
            "OrderProcessor.OrderCompleted -> IOrderProcessorEvt.OrderCompleted"
        };

        Assert.That(RelsOf(RelationshipType.Implements), Is.EquivalentTo(expected));
    }

    [Test]
    public void EventSubscriptionThroughInterfaceCast_IsDetectedAsHandles()
    {
        Assert.That(RelsOf(RelationshipType.Handles),
            Is.EquivalentTo(new[] { "OrderEventHandler.HandleOrderCompleted -> IOrderProcessorEvt.OrderCompleted" }));
    }

    [Test]
    public void EventInvocation_IsDetectedAsInvokes()
    {
        Assert.That(RelsOf(RelationshipType.Invokes),
            Is.EquivalentTo(new[] { "OrderProcessor.OnOrderCompleted -> OrderProcessor.OrderCompleted" }));
    }

    [Test]
    public void ObjectCreations_AreDetectedAsCreates()
    {
        var expected = new[]
        {
            "OrderProcessor.ProcessMultipleOrders -> OrderEventArgs",
            "Program.Main -> EmailNotificationService",
            "Program.Main -> OrderProcessor",
            "Program.Main -> OrderEventHandler"
        };

        Assert.That(RelsOf(RelationshipType.Creates), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodAndPropertyCalls_AreDetected()
    {
        var expected = new[]
        {
            "OrderEventArgs..ctor -> OrderEventArgs.OrderId",
            "OrderEventArgs..ctor -> OrderEventArgs.Amount",
            "OrderProcessor.ProcessMultipleOrders -> OrderProcessor.OnOrderCompleted",
            "OrderProcessor.ProcessMultipleOrders -> OrderEventArgs..ctor",
            "OrderEventHandler.HandleOrderCompleted -> INotificationService.SendNotification",
            "OrderEventHandler.HandleOrderCompleted -> OrderEventArgs.OrderId",
            "OrderEventHandler.HandleOrderCompleted -> OrderEventArgs.Amount",
            "OrderEventHandler.HandleOrderCompleted -> OrderEventHandler.LogOrderCompletion",
            "Program.Main -> OrderProcessor..ctor",
            "Program.Main -> OrderEventHandler..ctor",
            "Program.Main -> IOrderProcessor.ProcessMultipleOrders"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
