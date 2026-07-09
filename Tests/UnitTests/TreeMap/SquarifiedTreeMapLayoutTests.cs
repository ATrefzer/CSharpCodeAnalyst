using System.Windows;
using CSharpCodeAnalyst.TreeMap.Data;
using CSharpCodeAnalyst.TreeMap.TreeMap;

namespace CodeParserTests.UnitTests.TreeMap;

[TestFixture]
public class SquarifiedTreeMapLayoutTests
{
    private const double W = 100.0;
    private const double H = 80.0;

    /// <summary>root with four leaves of area metric 40 / 30 / 20 / 10.</summary>
    private static HierarchicalData BuildTree()
    {
        var root = new HierarchicalData("root");
        root.AddChild(new HierarchicalData("a", 40, 1));
        root.AddChild(new HierarchicalData("b", 30, 1));
        root.AddChild(new HierarchicalData("c", 20, 1));
        root.AddChild(new HierarchicalData("d", 10, 1));
        root.SumAreaMetrics();
        return root;
    }

    [Test]
    public void Layout_AssignsFullCanvasToRoot()
    {
        var root = BuildTree();

        var map = new SquarifiedTreeMapLayout().Layout(root, W, H);

        Assert.That(map.Get(root)!.Rect, Is.EqualTo(new Rect(0, 0, W, H)));
    }

    [Test]
    public void Layout_LeafRectanglesTileTheCanvasWithoutGaps()
    {
        var root = BuildTree();

        var map = new SquarifiedTreeMapLayout().Layout(root, W, H);

        var totalLeafArea = 0.0;
        foreach (var leaf in root.Children)
        {
            var rect = map.Get(leaf)!.Rect;
            Assert.That(rect.Width, Is.GreaterThan(0));
            Assert.That(rect.Height, Is.GreaterThan(0));
            Assert.That(rect.Left, Is.GreaterThanOrEqualTo(-1e-6));
            Assert.That(rect.Top, Is.GreaterThanOrEqualTo(-1e-6));
            Assert.That(rect.Right, Is.LessThanOrEqualTo(W + 1e-6));
            Assert.That(rect.Bottom, Is.LessThanOrEqualTo(H + 1e-6));
            totalLeafArea += rect.Width * rect.Height;
        }

        // Squarified layout is area-exact: the leaves fill the whole canvas.
        Assert.That(totalLeafArea, Is.EqualTo(W * H).Within(1e-4));
    }

    [Test]
    public void Layout_PixelAreaIsProportionalToAreaMetric()
    {
        var root = BuildTree();

        var map = new SquarifiedTreeMapLayout().Layout(root, W, H);

        double PixelArea(string name)
        {
            var node = root.Children.Single(c => c.Name == name);
            var rect = map.Get(node)!.Rect;
            return rect.Width * rect.Height;
        }

        // 'a' has area metric 40, 'd' has 10 -> four times the pixels.
        Assert.That(PixelArea("a") / PixelArea("d"), Is.EqualTo(4.0).Within(1e-6));
    }

    [Test]
    public void Layout_ReturnsFreshMapEachCall_NoStateLeakBetweenRuns()
    {
        var root = BuildTree();
        var layout = new SquarifiedTreeMapLayout();

        var first = layout.Layout(root, W, H);
        var second = layout.Layout(root, 2 * W, 2 * H);

        // The second run gets its own map; the first map keeps the first canvas.
        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.Get(root)!.Rect, Is.EqualTo(new Rect(0, 0, W, H)));
        Assert.That(second.Get(root)!.Rect, Is.EqualTo(new Rect(0, 0, 2 * W, 2 * H)));
    }
}
