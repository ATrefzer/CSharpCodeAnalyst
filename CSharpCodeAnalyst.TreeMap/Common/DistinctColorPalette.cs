using System.Windows.Media;

namespace CSharpCodeAnalyst.TreeMap.Common
{
    /// <summary>
    /// Generates an arbitrarily long sequence of colors that are as distinguishable as possible,
    /// for assigning a color to each item of a set whose size is not known in advance (e.g. one
    /// color per committer of a repository).
    ///
    /// The sequence is deterministic and prefix-stable: GetColors(n) is always a prefix of
    /// GetColors(n + k). Callers can therefore hand out colors incrementally - an item keeps its
    /// color no matter how many items are added later.
    ///
    /// Algorithm (farthest point sampling): candidates are a grid over the sRGB cube, converted
    /// to the CIELAB color space, where Euclidean distance approximates the PERCEIVED color
    /// difference. Each new color is the candidate whose minimum distance to all previously
    /// chosen colors is largest. White, black, light gray (default tile color) and yellow
    /// (highlight color) act as pre-chosen anchors, so the sequence automatically keeps its
    /// distance from backgrounds, borders and the hover highlight. Candidates close to black or
    /// white are excluded entirely via a lightness window.
    ///
    /// The more colors are requested, the smaller the pairwise distances become - that is
    /// inherent to the problem, but every prefix is close to the best possible choice for its
    /// size.
    /// </summary>
    public static class DistinctColorPalette
    {
        // Lightness window (CIELAB L, 0 = black, 100 = white). Colors outside are unusable on
        // a light background with black borders/text.
        private const double MinLightness = 25.0;
        private const double MaxLightness = 92.0;

        // Grid resolution of the sRGB candidate cube. Step 15 gives 18^3 = 5832 candidates,
        // fine enough that the greedy selection is not limited by the grid.
        private const int GridStep = 15;

        private static readonly Lock SyncRoot = new();
        private static readonly List<Color> Sequence = [];
        private static List<Candidate>? _candidates;

        /// <summary>
        /// Returns the first <paramref name="count" /> colors of the sequence. Thread-safe.
        /// Prefix-stable: the result for a smaller count is always a prefix of the result for a
        /// larger count.
        /// </summary>
        public static IReadOnlyList<Color> GetColors(int count)
        {
            lock (SyncRoot)
            {
                _candidates ??= CreateCandidates();

                while (Sequence.Count < count && _candidates.Count > 0)
                {
                    AppendFarthestCandidate();
                }

                return Sequence.Take(Math.Min(count, Sequence.Count)).ToList();
            }
        }

        private static void AppendFarthestCandidate()
        {
            var candidates = _candidates!;

            var bestIndex = 0;
            for (var i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].MinDistance > candidates[bestIndex].MinDistance)
                {
                    bestIndex = i;
                }
            }

            var best = candidates[bestIndex];
            candidates.RemoveAt(bestIndex);
            Sequence.Add(best.Color);

            // Every remaining candidate only has to check its distance against the new color -
            // the minimum against all older ones is already cached.
            foreach (var candidate in candidates)
            {
                var distance = Distance(candidate.Lab, best.Lab);
                if (distance < candidate.MinDistance)
                {
                    candidate.MinDistance = distance;
                }
            }
        }

        private static List<Candidate> CreateCandidates()
        {
            // Reserved colors the sequence must keep its distance from: white (background),
            // black (borders, text), light gray (DefaultDrawingPrimitives.DefaultColor) and
            // yellow (DefaultDrawingPrimitives.HighlightBrush).
            var anchors = new[]
            {
                ToLab(255, 255, 255),
                ToLab(0, 0, 0),
                ToLab(211, 211, 211),
                ToLab(255, 255, 0)
            };

            var candidates = new List<Candidate>();
            for (var r = 0; r <= 255; r += GridStep)
            {
                for (var g = 0; g <= 255; g += GridStep)
                {
                    for (var b = 0; b <= 255; b += GridStep)
                    {
                        var lab = ToLab(r, g, b);
                        if (lab.L is < MinLightness or > MaxLightness)
                        {
                            continue;
                        }

                        var minDistance = anchors.Min(anchor => Distance(lab, anchor));
                        candidates.Add(new Candidate
                        {
                            Color = Color.FromRgb((byte)r, (byte)g, (byte)b),
                            Lab = lab,
                            MinDistance = minDistance
                        });
                    }
                }
            }

            return candidates;
        }

        /// <summary>
        /// CIE76 color difference: Euclidean distance in CIELAB. Good enough here - the more
        /// elaborate CIEDE2000 formula would only refine distances between already-similar
        /// colors, which this palette avoids anyway.
        /// </summary>
        private static double Distance(LabColor x, LabColor y)
        {
            var dl = x.L - y.L;
            var da = x.A - y.A;
            var db = x.B - y.B;
            return Math.Sqrt(dl * dl + da * da + db * db);
        }

        /// <summary>
        /// sRGB (D65) to CIELAB. Standard formulas: gamma-decode, linear transform to XYZ,
        /// then the Lab companding function.
        /// </summary>
        private static LabColor ToLab(int r8, int g8, int b8)
        {
            var r = Linearize(r8 / 255.0);
            var g = Linearize(g8 / 255.0);
            var b = Linearize(b8 / 255.0);

            var x = (r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047;
            var y = r * 0.2126 + g * 0.7152 + b * 0.0722;
            var z = (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883;

            var fx = LabF(x);
            var fy = LabF(y);
            var fz = LabF(z);

            return new LabColor(116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
        }

        private static double Linearize(double c)
        {
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static double LabF(double t)
        {
            return t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116.0;
        }

        private readonly record struct LabColor(double L, double A, double B);

        private sealed class Candidate
        {
            public Color Color { get; init; }
            public LabColor Lab { get; init; }
            public double MinDistance { get; set; }
        }
    }
}
