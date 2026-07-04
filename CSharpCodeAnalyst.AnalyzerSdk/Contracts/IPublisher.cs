namespace CSharpCodeAnalyst.AnalyzerSdk.Contracts;

public interface IPublisher
{
    void Publish<TMessage>(TMessage message) where TMessage : class;
}