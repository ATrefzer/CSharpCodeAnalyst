using System.Windows.Media;
using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

public interface IRelationshipContextCommand
{
    string Label { get; }
    ImageSource? Icon { get; }

    bool CanHandle(List<Relationship> relationships);
    void Invoke(List<Relationship> relationships);
}