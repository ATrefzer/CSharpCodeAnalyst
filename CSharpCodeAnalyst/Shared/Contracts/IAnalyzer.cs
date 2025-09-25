using Contracts.Graph;

namespace CSharpCodeAnalyst.Shared.Contracts;

public interface IAnalyzer
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    void Analyze(CodeGraph graph);
}