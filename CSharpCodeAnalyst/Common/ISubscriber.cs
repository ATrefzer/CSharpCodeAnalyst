namespace CSharpCodeAnalyst.Common;

public interface ISubscriber
{
    void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
    void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
}