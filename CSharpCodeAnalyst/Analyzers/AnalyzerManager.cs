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

    public void LoadAnalyzers(IPublisher messaging)
    {
        _analyzers.Clear();
        var analyzer = new Analyzer(messaging);
        _analyzers.Add(analyzer.Id, analyzer);
    }
}