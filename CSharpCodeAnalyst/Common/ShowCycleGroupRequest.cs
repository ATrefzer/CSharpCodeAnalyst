using CodeParser.Analysis.Shared;

namespace CSharpCodeAnalyst.Common;

public class ShowCycleGroupRequest(CycleGroup cycleGroup)
{
    public CycleGroup CycleGroup { get; } = cycleGroup;
}