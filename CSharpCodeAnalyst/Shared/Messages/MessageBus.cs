using CSharpCodeAnalyst.Shared.Contracts;

namespace CSharpCodeAnalyst.Common;

public class MessageBus : ISubscriber, IPublisher
{
    private readonly object _lock = new();
    private readonly Dictionary<Type, List<Delegate>> _typeToSubscribersMap = new();

    public void Publish<TMessage>(TMessage message) where TMessage : class
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        List<Delegate> handlers;

        var messageType = typeof(TMessage);

        lock (_lock)
        {
            if (_typeToSubscribersMap.TryGetValue(messageType, out var subscribers))
            {
                handlers = [..subscribers];
            }
            else
            {
                return;
            }
        }

        foreach (var handler in handlers)
        {
            ((Action<TMessage>)handler)(message);
        }
    }

    public void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var messageType = typeof(TMessage);

        lock (_lock)
        {
            if (!_typeToSubscribersMap.ContainsKey(messageType))
            {
                _typeToSubscribersMap[messageType] = [];
            }

            _typeToSubscribersMap[messageType].Add(handler);
        }
    }

    public void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var messageType = typeof(TMessage);

        lock (_lock)
        {
            if (!_typeToSubscribersMap.TryGetValue(messageType, out var value))
            {
                return;
            }

            value.Remove(handler);
            if (_typeToSubscribersMap[messageType].Count == 0)
            {
                _typeToSubscribersMap.Remove(messageType);
            }
        }
    }
}