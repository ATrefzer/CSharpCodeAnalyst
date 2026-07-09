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

    public HotspotNode AnalyzeHotspots(ChangeSetHistory changeSets, Dictionary<string, LinesOfCodeProvider.LinesOfCode> linesOfCode)
    {
        // We process only files we have a metric for.        
        var filter = new FileFilter(linesOfCode.Keys);
        
        // Only files we have lines of code calculated.
        var summary = changeSets.GetArtifactSummary(filter, new NullAliasMapping());
        
        var builder = new HotspotBuilder();
        return builder.Build(summary, linesOfCode);
    }

    public HotspotNode AnalyzeKnowledge(ChangeSetHistory changeSets, Dictionary<string, LinesOfCodeProvider.LinesOfCode> linesOfCode, Dictionary<string, Contribution> contribution)
    {
      

        // We process only files we have a metric for.        
        var filter = new FileFilter(linesOfCode.Keys);
        
        // Only files we have lines of code calculated.
        var summary = changeSets.GetArtifactSummary(filter, new NullAliasMapping());

        // Keep the case-insensitive path-key contract when projecting to main developers -
        // a plain ToDictionary would fall back to the default, case-sensitive comparer.
        var fileToMainDeveloper = contribution
            .ToDictionary(pair => pair.Key, pair => pair.Value.GetMainDeveloper(), StringComparer.OrdinalIgnoreCase);

        // Build the knowledge data
        var builder = new KnowledgeBuilder();
        return builder.Build(summary, linesOfCode, fileToMainDeveloper);
    }
}