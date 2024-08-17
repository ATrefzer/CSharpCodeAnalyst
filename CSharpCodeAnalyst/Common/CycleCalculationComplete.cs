using CodeParser.Analysis.Shared;

namespace CSharpCodeAnalyst.Common;

public class CycleCalculationComplete(List<CycleGroup> cycleGroups)
{
    public List<CycleGroup> CycleGroups { get; } = cycleGroups;
}