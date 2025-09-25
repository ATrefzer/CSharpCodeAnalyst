namespace CSharpCodeAnalyst.Shared.Messaging;

public interface IPublisher
{
    void Publish<TMessage>(TMessage message) where TMessage : class;
}