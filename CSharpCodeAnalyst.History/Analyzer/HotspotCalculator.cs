using CSharpCodeAnalyst.History.Metrics;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Analyzer
{
    public sealed class HotspotCalculator
    {
        readonly double _maxCommits = double.MinValue;
        readonly double _minCommits = double.MaxValue;
        readonly double _minLinesOfCode = double.MaxValue;
        readonly double _maxLinesOfCode = double.MinValue;
        readonly Dictionary<string, LinesOfCodeProvider.LinesOfCode> _metrics;

        public HotspotCalculator(IEnumerable<Artifact> artifacts, Dictionary<string, LinesOfCodeProvider.LinesOfCode> metrics)
        {
            // The metrics dictionary is already keyed case-insensitively by the time it reaches an
            // analyzer - freshly built by LinesOfCodeProvider, or normalized right after loading a
            // project (see HistoryViewModel.OnLoad). So a plain path lookup is enough here.
            _metrics = metrics;
            foreach (var artifact in artifacts)
            {
                _maxCommits = Math.Max(_maxCommits, GetCommits(artifact));
                _minCommits = Math.Min(_minCommits, GetCommits(artifact));
                _maxLinesOfCode = Math.Max(_maxLinesOfCode, GetLinesOfCode(artifact));
                _minLinesOfCode = Math.Min(_minLinesOfCode, GetLinesOfCode(artifact));
            }
        }

        /// <summary>
        /// Lines of Code
        /// </summary>
        public double GetLinesOfCode(Artifact item)
        {
            return _metrics.TryGetValue(item.LocalPath, out var loc) ? loc.Code : 0.0;
        }

        /// <summary>
        /// Commits
        /// </summary>
        public double GetCommits(Artifact item)
        {
            var weight = item.Commits;
            return weight;
        }

        public double GetHotspotValue(Artifact item)
        {
            // Calculate hotspot index
            var normalizedWeight = (GetCommits(item) - _minCommits) / (_maxCommits - _minCommits);
            var normalizedArea = (GetLinesOfCode(item) - _minLinesOfCode) / (_maxLinesOfCode - _minLinesOfCode);
            var hotspot = normalizedWeight * normalizedArea;
            return hotspot;
        }
    }
}
