using System.Windows.Media;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public interface IRelationshipContextCommand
{
    string Label { get; }
    ImageSource? Icon { get; }

    string? SubMenuGroup { get; }

    bool CanHandle(List<Relationship> relationships);
    void Invoke(string sourceId, string targetId, List<Relationship> relationships);
}