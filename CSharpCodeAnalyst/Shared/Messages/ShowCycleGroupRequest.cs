using CodeGraph.Algorithms.Cycles;

namespace CSharpCodeAnalyst.Shared.Messages;

public class ShowCycleGroupRequest(CycleGroup cycleGroup)
{
    public CycleGroup CycleGroup { get; } = cycleGroup;
}