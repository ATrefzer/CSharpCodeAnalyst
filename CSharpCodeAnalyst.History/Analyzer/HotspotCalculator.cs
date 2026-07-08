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
            // GetLinesOfCode looks up by a lower-cased key, but the incoming dictionary (fresh from
            // LinesOfCodeProvider, or round-tripped through JSON - which never preserves a custom
            // comparer) uses ordinal, case-sensitive keys. Without this the lookup misses almost
            // every file, GetLinesOfCode silently returns 0 for everything, and every artifact fails
            // IsAccepted - the tree ends up with no leaves at all.
            _metrics = new Dictionary<string, LinesOfCodeProvider.LinesOfCode>(metrics, StringComparer.OrdinalIgnoreCase);
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
            var area = 0.0;
            var key = item.LocalPath.ToLowerInvariant();

            if (_metrics.ContainsKey(key))
            {
                // Lines of code
                area = _metrics[key].Code;
            }

            return area;
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
