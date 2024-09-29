using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface IRelationshipContextCommand
{
    string Label { get; }

    bool CanHandle(List<Relationship> relationships);
    void Invoke(List<Relationship> dependencies);
}