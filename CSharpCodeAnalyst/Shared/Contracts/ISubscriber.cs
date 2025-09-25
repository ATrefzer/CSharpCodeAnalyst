namespace CSharpCodeAnalyst.Shared.Contracts;

public interface ISubscriber
{
    void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
    void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
}