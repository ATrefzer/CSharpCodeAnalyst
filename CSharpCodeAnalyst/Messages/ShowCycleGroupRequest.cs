using CodeGraph.Algorithms.Cycles;

namespace CSharpCodeAnalyst.Messages;

public class ShowCycleGroupRequest(CycleGroup cycleGroup)
{
    public CycleGroup CycleGroup { get; } = cycleGroup;
}