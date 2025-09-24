using CodeParser.Analysis.Shared;

namespace CSharpCodeAnalyst.Messages;

public class ShowCycleGroupRequest(CycleGroup cycleGroup)
{
    public CycleGroup CycleGroup { get; } = cycleGroup;
}