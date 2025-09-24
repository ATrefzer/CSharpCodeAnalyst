namespace CSharpCodeAnalyst.Common;

public interface IPublisher
{
    void Publish<TMessage>(TMessage message) where TMessage : class;
}