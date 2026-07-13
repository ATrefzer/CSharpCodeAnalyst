using System.IO.Pipes;
using CSharpCodeAnalyst.Contracts;
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

    public IHierarchicalData AnalyzeHotspots(ChangeSetHistory changeSets, Dictionary<string, LinesOfCodeProvider.LinesOfCode> linesOfCode)
    {
        // We process only files we have a metric for.
        var filter = new FileFilter(linesOfCode.Keys);

        // Hotspots plot commit frequency against size - no developer identity is involved,
        // so the alias mapping has nothing to act on here.
        var summary = changeSets.GetArtifactSummary(filter);

        var builder = new HotspotBuilder();
        return builder.Build(summary, linesOfCode);
    }

    public IHierarchicalData AnalyzeKnowledge(ChangeSetHistory changeSets, Dictionary<string, LinesOfCodeProvider.LinesOfCode> linesOfCode, Dictionary<string, Contribution> contribution, IAliasMapping aliasMapping)
    {
        // We process only files we have a metric for.
        var filter = new FileFilter(linesOfCode.Keys);

        // The summary only feeds commit counts / paths into the map.
        var summary = changeSets.GetArtifactSummary(filter);

        // Keep the case-insensitive path-key contract when projecting to main developers.
        // The per-developer contributions are aggregated by alias here. So the main developer becomes the main alias,
        // e.g. the team that owns the file.
        var aliasMappedContributions = contribution.ToDictionary(pair => pair.Key, pair => AggregateByAlias(pair.Value, aliasMapping));
        var fileToMainDeveloper = aliasMappedContributions
            .ToDictionary(pair => pair.Key, pair => pair.Value.GetMainDeveloper(), StringComparer.OrdinalIgnoreCase);

        // Build the knowledge data
        var builder = new KnowledgeBuilder();
        return builder.Build(summary, linesOfCode, fileToMainDeveloper);
    }

    /// <summary>
    ///     Collapses the per-developer contributions of a single file onto their aliases by summing
    ///     the line counts of all developers that share an alias. Developers without a mapping keep
    ///     their own name (see <see cref="IAliasMapping.GetAlias" />).
    /// </summary>
    private static Contribution AggregateByAlias(Contribution contribution, IAliasMapping aliasMapping)
    {
        var aggregated = new Dictionary<string, uint>();
        foreach (var pair in contribution.DeveloperToContribution)
        {
            var alias = aliasMapping.GetAlias(pair.Key);
            aggregated[alias] = aggregated.TryGetValue(alias, out var existing) ? existing + pair.Value : pair.Value;
        }

        return new Contribution(aggregated);
    }
}