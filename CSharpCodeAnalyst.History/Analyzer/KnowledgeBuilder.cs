using CSharpCodeAnalyst.History.Metrics;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Analyzer;

/// <summary>
///     Transforms the given artifact summary, code metrics and main developer per file into a knowledge map.
/// </summary>
public sealed class KnowledgeBuilder : BuilderBase
{
    private Dictionary<string, MainDeveloper>? _mainDeveloper;
    private Dictionary<string, LinesOfCodeProvider.LinesOfCode>? _metrics;
    private readonly string? _onlyThisDeveloper;

    public KnowledgeBuilder()
    {
        _onlyThisDeveloper = null;
    }

    public KnowledgeBuilder(string developer)
    {
        _onlyThisDeveloper = developer;
    }

    public HotspotNode Build(List<Artifact> summary,
        Dictionary<string, LinesOfCodeProvider.LinesOfCode> metrics,
        Dictionary<string, MainDeveloper> mainDeveloper)
    {
        // Both dictionaries are already keyed case-insensitively when they reach here - freshly
        // built, or normalized right after loading a project (see HistoryViewModel.OnLoad). So
        // plain path lookups are enough in GetArea / GetMainDeveloper.
        _metrics = metrics;
        _mainDeveloper = mainDeveloper;
        return Build(summary);
    }


    protected override HotspotNode CreateLeafNode(string leafName, Artifact item)
    {
        // Color comes from the main developer (ColorKey); the weight carries the ownership
        // percentage so the filter slider shows a meaningful value instead of NaN.
        var leaf = new HotspotNode(leafName, GetArea(item), GetColorKey(item), GetMainDeveloper(item).Percent)
        {
            Description = GetDescription(item),
            Tag = item.LocalPath
        };
        return leaf;
    }

    private double GetArea(Artifact item)
    {
        ArgumentNullException.ThrowIfNull(_metrics);
        return _metrics.TryGetValue(item.LocalPath, out var loc) ? loc.Code : 0.0;
    }

    private string GetColorKey(Artifact item)
    {
        var mainDev = GetMainDeveloper(item).Developer;

        if (_onlyThisDeveloper != null && mainDev != _onlyThisDeveloper)
        {
            // Default color for all artifacts not provided by the developer of interest.
            return "";
        }

        return mainDev;
    }

    private string GetDescription(Artifact item)
    {
        var mainDev = GetMainDeveloper(item);
        return item.ServerPath + "\nCommits: " + item.Commits
               + "\nLOC: " + GetArea(item)
               + "\nMain developer: " + mainDev.Developer + " " + mainDev.Percent.ToString("F2") + "%";
    }

    protected override bool IsAccepted(Artifact item)
    {
        // Area must > 0 because of division.
        var area = GetArea(item);

        return area >= 1;
    }

    private MainDeveloper GetMainDeveloper(Artifact item)
    {
        ArgumentNullException.ThrowIfNull(_mainDeveloper);
        
        // Default (empty developer, maps to the neutral brush) when the file has no contribution.
        return _mainDeveloper.TryGetValue(item.LocalPath, out var dev) ? dev : new MainDeveloper("", 0.0);
    }
}