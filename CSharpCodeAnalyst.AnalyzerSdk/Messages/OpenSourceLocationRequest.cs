using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.AnalyzerSdk.Messages;

public class OpenSourceLocationRequest(SourceLocation location)
{
    public SourceLocation Location { get; } = location;
}
