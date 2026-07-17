using System.Windows.Media;
using CSharpCodeAnalyst.TreeMap.Common;

namespace CodeParserTests.UnitTests.TreeMap;

[TestFixture]
public class DistinctColorPaletteTests
{
    [Test]
    public void GetColors_ReturnsRequestedCount()
    {
        Assert.That(DistinctColorPalette.GetColors(0), Is.Empty);
        Assert.That(DistinctColorPalette.GetColors(1), Has.Count.EqualTo(1));
        Assert.That(DistinctColorPalette.GetColors(50), Has.Count.EqualTo(50));
    }

    [Test]
    public void GetColors_IsPrefixStable()
    {
        // The core property for incremental assignment: asking for more colors later must not
        // change the colors already handed out.
        var few = DistinctColorPalette.GetColors(8);
        var many = DistinctColorPalette.GetColors(32);

        Assert.That(many.Take(8), Is.EqualTo(few));
    }

    [Test]
    public void GetColors_AllColorsAreUnique()
    {
        var colors = DistinctColorPalette.GetColors(64);

        Assert.That(colors.Distinct().Count(), Is.EqualTo(64));
    }

    [Test]
    public void GetColors_PairwisePerceptualDistanceStaysAboveFloor()
    {
        // Thresholds calibrated against the implementation (measured: 43.9 for n=16,
        // 32.4 for n=32) with a safety margin. dE >= 25 is comfortably distinguishable.
        Assert.That(MinPairwiseDistance(DistinctColorPalette.GetColors(16)), Is.GreaterThan(40));
        Assert.That(MinPairwiseDistance(DistinctColorPalette.GetColors(32)), Is.GreaterThan(25));
    }

    [Test]
    public void GetColors_SmallSetsAreStronglySeparated()
    {
        // Regression guard for the "red + pink at n=3" defect: using yellow as an FPS repulsion
        // anchor pushed the whole sequence away from the yellow-green region, so green could not
        // be chosen early and the red-blue gap was filled with pink (dE ~ 90). With yellow as a
        // pure exclusion zone the first three colors are blue/green/red, all far apart.
        Assert.That(MinPairwiseDistance(DistinctColorPalette.GetColors(3)), Is.GreaterThan(120));
    }

    [Test]
    public void GetColors_KeepsDistanceFromBackgroundBorderAndHighlightColors()
    {
        // White (background), black (borders/text), light gray (default tile) and yellow
        // (hover highlight) are reserved - no generated color may come close to them.
        var anchors = new[]
        {
            ToLab(Colors.White),
            ToLab(Colors.Black),
            ToLab(Colors.LightGray),
            ToLab(Colors.Yellow)
        };

        var colors = DistinctColorPalette.GetColors(32);

        var minDistance = colors.Min(c => anchors.Min(a => Distance(ToLab(c), a)));
        Assert.That(minDistance, Is.GreaterThan(25));
    }

    [Test]
    public void GetColors_LightnessStaysInReadableWindow()
    {
        // No near-black and no near-white colors: they would vanish against borders/background.
        var colors = DistinctColorPalette.GetColors(64);

        foreach (var color in colors)
        {
            var lightness = ToLab(color).L;
            Assert.That(lightness, Is.InRange(25.0, 92.0), $"Color {color} is too dark or too light");
        }
    }

    // ---------------------------------------------------------------------
    // Independent copy of the sRGB -> CIELAB conversion, so the tests do not rely on the
    // implementation under test for their own measurements.
    // ---------------------------------------------------------------------

    private readonly record struct Lab(double L, double A, double B);

    private static double MinPairwiseDistance(IReadOnlyList<Color> colors)
    {
        var labs = colors.Select(ToLab).ToList();
        var min = double.MaxValue;
        for (var i = 0; i < labs.Count; i++)
        {
            for (var j = i + 1; j < labs.Count; j++)
            {
                min = Math.Min(min, Distance(labs[i], labs[j]));
            }
        }

        return min;
    }

    private static double Distance(Lab x, Lab y)
    {
        var dl = x.L - y.L;
        var da = x.A - y.A;
        var db = x.B - y.B;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    private static Lab ToLab(Color color)
    {
        var r = Linearize(color.R / 255.0);
        var g = Linearize(color.G / 255.0);
        var b = Linearize(color.B / 255.0);

        var x = (r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047;
        var y = r * 0.2126 + g * 0.7152 + b * 0.0722;
        var z = (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883;

        var fx = LabF(x);
        var fy = LabF(y);
        var fz = LabF(z);

        return new Lab(116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }

    private static double Linearize(double c)
    {
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double LabF(double t)
    {
        return t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116.0;
    }
}
