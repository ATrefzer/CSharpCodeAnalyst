using CSharpCodeAnalyst.Shared.Contracts;

namespace CSharpCodeAnalyst.Analyzers
{
    internal class AnalyzerManager : IAnalyzerManager
    {
        private static readonly IAnalyzerManager _instance = new AnalyzerManager();
        private Dictionary<string, IAnalyzer> _analyzers = [];

        public IAnalyzer GetAnalyzer(string id)
        {
            return _analyzers[id];
        }

        public IEnumerable<IAnalyzer> All => _analyzers.Values.ToList();

        public void LoadAnalyzers(IPublisher messaging)
        {
            _analyzers.Clear();
            var analyzer = new Analyzer.EventRegistration.Analyzer(messaging);
            _analyzers.Add(analyzer.Id, analyzer);
        }
    }
}