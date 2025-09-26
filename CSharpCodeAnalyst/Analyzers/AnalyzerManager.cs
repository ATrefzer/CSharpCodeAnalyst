using CSharpCodeAnalyst.Analyzers.EventRegistration;
using CSharpCodeAnalyst.Analyzers.ConsistencyRules;
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

    public void LoadAnalyzers(IPublisher messaging)
    {
        _analyzers.Clear();
        var eventAnalyzer = new EventRegistration.Analyzer(messaging);
        _analyzers.Add(eventAnalyzer.Id, eventAnalyzer);

        var consistencyAnalyzer = new ConsistencyRules.Analyzer(messaging);
        _analyzers.Add(consistencyAnalyzer.Id, consistencyAnalyzer);
    }

    /// <summary>
    /// Collects persistent data from all analyzers
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
    /// Restores persistent data to all analyzers
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
}