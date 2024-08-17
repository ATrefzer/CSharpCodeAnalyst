namespace CSharpCodeAnalyst.GraphArea;

public interface IContextCommand
{
    string Label { get; }

    bool CanHandle(object item);
    void Invoke(object item);
}