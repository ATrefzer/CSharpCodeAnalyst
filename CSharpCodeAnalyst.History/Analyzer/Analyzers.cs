using CSharpCodeAnalyst.History.Metrics;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Analyzer;

public class Analyzers
{
    public List<Coupling> AnalyzeChangeCoupling(ChangeSetHistory history)
    {
        // Pair wise couplings
        var couplingAnalyzer = new ChangeCouplingAnalyzer();
        var couplings = couplingAnalyzer.CalculateChangeCouplings(history);
        var sortedCouplings = couplings.OrderByDescending(coupling => coupling.Degree).ToList();
        return sortedCouplings;
    }

    public HotspotNode AnalyzeHotspots(ChangeSetHistory history, Dictionary<string, LinesOfCodeProvider.LinesOfCode> linesOfCode)
    {
        // We process only files we have a metric for.        
        var filter = new FileFilter(linesOfCode.Keys);
        
        // Only files we have lines of code calculated.
        var summary = history.GetArtifactSummary(filter, new NullAliasMapping());
        
        var builder = new HotspotBuilder();
        return builder.Build(summary, linesOfCode);
    }
}