using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Shared.Messages;

public class OpenSourceLocationRequest(SourceLocation location)
{
    public SourceLocation Location { get; } = location;
}
