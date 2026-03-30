using CodeGraph.Algorithms.Cycles;

namespace CSharpCodeAnalyst.Shared.Messages;

public class CycleCalculationComplete(List<CycleGroup> cycleGroups)
{
    public List<CycleGroup> CycleGroups { get; } = cycleGroups;
}