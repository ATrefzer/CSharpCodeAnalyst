using System.Globalization;
using CSharpCodeAnalyst.History.Config;
using CSharpCodeAnalyst.History.Metrics;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Analyzer;

/// <summary>
///     Builds a hotspot tree (folder hierarchy, area = lines of code, weight = commits) from a flat
///     artifact summary. Standalone on purpose - see the note on <see cref="HotspotNode" />.
/// </summary>
public sealed class HotspotBuilder : BuilderBase
{
    private HotspotCalculator _hotspotCalculator = null!;

    public HotspotNode Build(List<Artifact> artifacts, Dictionary<string, LinesOfCodeProvider.LinesOfCode> metrics)
    {
        _hotspotCalculator = new HotspotCalculator(artifacts, metrics);

        return Build(artifacts);
    }

    private double GetArea(Artifact item)
    {
        return _hotspotCalculator.GetLinesOfCode(item);
    }
    
    private string GetDescription(Artifact item)
    {
        var hotspot = _hotspotCalculator.GetHotspotValue(item);
        return item.ServerPath
               + "\nCommits: " + item.Commits
               + "\nLOC: " + _hotspotCalculator.GetLinesOfCode(item)
               + "\nHotspot: " + hotspot.ToString("F5", CultureInfo.InvariantCulture);
    }

    private double GetWeight(Artifact item)
    {
        return _hotspotCalculator.GetCommits(item);
    }


    protected override HotspotNode CreateLeafNode(string leafName, Artifact item)
    {
        var leaf = new HotspotNode(leafName, GetArea(item), GetWeight(item))
        {
            Description = GetDescription(item),
            Tag = item.LocalPath
        };
        return leaf;
    }

    protected override bool IsAccepted(Artifact item)
    {
        // Area must be > 0 because of division. A file must have a size (lines of code) and must
        // have been committed often enough to be relevant.
        return GetArea(item) >= Thresholds.MinLinesOfCodeForHotspot && GetWeight(item) >= Thresholds.MinCommitsForHotspots;
    }
}