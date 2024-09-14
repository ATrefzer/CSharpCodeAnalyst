using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface IDependencyContextCommand
{
    string Label { get; }

    bool CanHandle(List<Dependency> dependencies);
    void Invoke(List<Dependency> dependencies);
}