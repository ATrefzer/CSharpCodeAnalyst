using Contracts.Graph;

namespace CSharpCodeAnalyst.Common;

public class AddNodeToGraphRequest
{
    public AddNodeToGraphRequest(CodeElement node)
    {
        Nodes = [node];
    }

    public AddNodeToGraphRequest(IEnumerable<CodeElement> nodes)
    {
        Nodes = nodes.ToList();
    }

    public IReadOnlyList<CodeElement> Nodes { get; }
}