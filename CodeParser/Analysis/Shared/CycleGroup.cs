using Contracts.Graph;

namespace CodeParser.Analysis.Shared;

public class CycleGroup(CodeGraph codeGraph)
{
    public CodeGraph CodeGraph { get; } = codeGraph;
}