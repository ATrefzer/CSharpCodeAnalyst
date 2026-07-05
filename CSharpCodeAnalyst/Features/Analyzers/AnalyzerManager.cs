using CSharpCodeAnalyst.Analyzers.EventRegistration;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Notifications;
using ArchitecturalRules = CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using MethodComplexity = CSharpCodeAnalyst.Analyzers.MethodComplexity;
using SystemMetrics = CSharpCodeAnalyst.Analyzers.SystemMetrics;
using TypeCohesion = CSharpCodeAnalyst.Analyzers.TypeCohesion;
using TypeDependencies = CSharpCodeAnalyst.Analyzers.TypeDependencies;

namespace CSharpCodeAnalyst.Features.Analyzers;

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
    ///     Restores persistent data to all analyzers.
    ///     Analyzers without an entry in the given data are reset. Otherwise they would
    ///     keep the state of the previously loaded project (e.g. architectural rules).
    /// </summary>
    public void RestoreAnalyzerData(Dictionary<string, string> data)
    {
        foreach (var analyzer in _analyzers.Values)
        {
            data.TryGetValue(analyzer.Id, out var analyzerData);
            analyzer.SetPersistentData(analyzerData);
        }
    }

    public event EventHandler? AnalyzerDataChanged;

    private void RaiseAnalyzerDataChanged()
    {
        AnalyzerDataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void LoadAnalyzers(IPublisher messaging, IUserNotification userNotification, MetricStore metricStore)
    {
        _analyzers.Clear();

        IAnalyzer analyzer = new ArchitecturalRules.Analyzer(messaging, userNotification);
        analyzer.DataChanged += (_, _) => RaiseAnalyzerDataChanged();
        _analyzers.Add(analyzer.Id, analyzer);
        
        analyzer = new Analyzer(messaging, userNotification);
        analyzer.DataChanged += (_, _) => RaiseAnalyzerDataChanged();
        _analyzers.Add(analyzer.Id, analyzer);
        
        analyzer = new SystemMetrics.Analyzer(messaging, userNotification);
        analyzer.DataChanged += (_, _) => RaiseAnalyzerDataChanged();
        _analyzers.Add(analyzer.Id, analyzer);
        
        analyzer = new TypeDependencies.Analyzer(messaging, userNotification);
        analyzer.DataChanged += (_, _) => RaiseAnalyzerDataChanged();
        _analyzers.Add(analyzer.Id, analyzer);

        analyzer = new TypeCohesion.Analyzer(messaging, userNotification);
        analyzer.DataChanged += (_, _) => RaiseAnalyzerDataChanged();
        _analyzers.Add(analyzer.Id, analyzer);

        analyzer = new MethodComplexity.Analyzer(messaging, userNotification, metricStore);
        analyzer.DataChanged += (_, _) => RaiseAnalyzerDataChanged();
        _analyzers.Add(analyzer.Id, analyzer);

       
    }

    public bool IsDirty()
    {
        return _analyzers.Values.Any(a => a.IsDirty());
    }
}