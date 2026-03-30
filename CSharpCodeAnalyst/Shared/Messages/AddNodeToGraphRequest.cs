using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Shared.Messages;

public class AddNodeToGraphRequest
{

    public AddNodeToGraphRequest(CodeElement node)
    {
        Nodes = [node];
    }

    public AddNodeToGraphRequest(IEnumerable<CodeElement> nodes, bool addCollapsed)
    {
        AddCollapsed = addCollapsed;
        Nodes = nodes.ToList();
    }

    public bool AddCollapsed { get; }

    public IReadOnlyList<CodeElement> Nodes { get; }
}