using Contracts.Graph;

namespace CSharpCodeAnalyst.Common;

public class AddNodeToGraphRequest
{
    public bool AddCollapsed { get; }

    public AddNodeToGraphRequest(CodeElement node)
    {
        Nodes = [node];
    }

    public AddNodeToGraphRequest(IEnumerable<CodeElement> nodes, bool addCollapsed)
    {
        AddCollapsed = addCollapsed;
        Nodes = nodes.ToList();
    }

    public IReadOnlyList<CodeElement> Nodes { get; }
}