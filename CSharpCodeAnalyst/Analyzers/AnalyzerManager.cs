using CSharpCodeAnalyst.Analyzers.EventRegistration;
using CSharpCodeAnalyst.Shared.Contracts;

namespace CSharpCodeAnalyst.Analyzers;

internal class AnalyzerManager : IAnalyzerManager
{
    private readonly Dictionary<string, IAnalyzer> _analyzers = [];

    public IAnalyzer GetAnalyzer(string id)
    {
        return _analyzers[id];
    }

    public IEnumerable<IAnalyzer> All
    {
        get => _analyzers.Values.ToList();
    }

    /// <summary>
    ///     Collects persistent data from all analyzers
    /// </summary>
    public Dictionary<string, string> CollectAnalyzerData()
    {
        var data = new Dictionary<string, string>();

        foreach (var analyzer in _analyzers.Values)
        {
            var analyzerData = analyzer.GetPersistentData();
            if (!string.IsNullOrEmpty(analyzerData))
            {
                data[analyzer.Id] = analyzerData;
            }
        }

        return data;
    }

    /// <summary>
    ///     Restores persistent data to all analyzers
    /// </summary>
    public void RestoreAnalyzerData(Dictionary<string, string> data)
    {
        foreach (var analyzer in _analyzers.Values)
        {
            if (data.TryGetValue(analyzer.Id, out var analyzerData))
            {
                analyzer.SetPersistentData(analyzerData);
            }
        }
    }

    public void LoadAnalyzers(IPublisher messaging)
    {
        _analyzers.Clear();
        IAnalyzer analyzer = new Analyzer(messaging);
        _analyzers.Add(analyzer.Id, analyzer);

        analyzer = new ArchitecturalRules.Analyzer(messaging);
        _analyzers.Add(analyzer.Id, analyzer);
    }
}