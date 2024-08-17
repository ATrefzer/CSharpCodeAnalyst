using Contracts.Graph;

namespace CSharpCodeAnalyst.Common;

public class AddNodeToGraphRequest
{
    public AddNodeToGraphRequest(CodeElement node)
    {
        Node = node;
    }


    public CodeElement Node { get; }
}