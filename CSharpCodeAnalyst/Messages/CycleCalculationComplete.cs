using CodeParser.Analysis.Shared;

namespace CSharpCodeAnalyst.Messages;

public class CycleCalculationComplete(List<CycleGroup> cycleGroups)
{
    public List<CycleGroup> CycleGroups { get; } = cycleGroups;
}