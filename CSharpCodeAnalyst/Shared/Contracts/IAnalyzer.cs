using Contracts.Graph;

namespace CSharpCodeAnalyst.Shared.Contracts;

public interface IAnalyzer
{
    string Id { get; }
    void Analyze(CodeGraph graph);
    string Name { get; }
    string Description { get; }
}