using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CSharpCodeAnalyst.TreeMap.Interfaces;
using Brushes = System.Windows.Media.Brushes;
using ColorConverter = CSharpCodeAnalyst.TreeMap.Common.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using FormattedText = System.Windows.Media.FormattedText;

namespace CSharpCodeAnalyst.TreeMap.Bitmap;

public class LegendBitmap
{
    private readonly IBrushFactory _brushFactory;
    private readonly List<string> _names;

    public LegendBitmap(List<string> names, IBrushFactory brushFactory)
    {
        _names = names;
        _brushFactory = brushFactory;
    }

    public void CreateLegendBitmap(string file)
    {
        // WPF Visual für das Rendering
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Weißer Hintergrund
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 2000, 2000));

            var line = 0;
            Debug.Assert(_names.Count > 0);

            // Schriftart für WPF
            var typeface = new Typeface(new FontFamily("Arial"), FontStyles.Normal,
                FontWeights.Normal, FontStretches.Normal);
            var dpi = VisualTreeHelper.GetDpi(visual).PixelsPerDip;

            foreach (var name in _names)
            {
                // Legend
                var x = 0;
                var y = 30 * line;

                const int offsetColorName = 25;
                const int offsetDeveloperName = 200;

                var brush = _brushFactory.GetBrush(name);

                // Farbquadrat zeichnen
                dc.DrawRectangle(brush, new Pen(Brushes.Black, 1), new Rect(x, y, 20, 20));

                // Farbnamen-Text
                var colorText = "(" + GetColorName(name) + ")";
                var formattedColorText = new FormattedText(
                    colorText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12,
                    Brushes.Black,
                    dpi);
                dc.DrawText(formattedColorText, new Point(x + offsetColorName, y));

                // Entwicklernamen-Text
                var formattedNameText = new FormattedText(
                    name,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12,
                    Brushes.Black,
                    dpi);
                dc.DrawText(formattedNameText, new Point(x + offsetDeveloperName, y));

                line++;
            }
        }

        // Bitmap rendern
        var renderTarget = new RenderTargetBitmap(2000, 2000, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(visual);

        // Trimmen und speichern
        var trimmed = BitmapManipulation.TrimBitmap(renderTarget);
        if (trimmed != null)
        {
            SaveBitmap(trimmed, file);
        }
    }

    public void CreateLegendText(string path)
    {
        using (var file = File.CreateText(path))
        {
            foreach (var name in _names) // dump only used developers!
            {
                var colorName = GetColorName(name);
                file.WriteLine(name + "\t" + colorName);
            }
        }
    }

    private string GetColorName(string name)
    {
        var brush = _brushFactory.GetBrush(name);
        var argb = ColorConverter.ToArgb(brush.Color);
        var colorName = "#" + argb.ToString("X");
        return colorName;
    }

    private void SaveBitmap(BitmapSource bitmap, string filename)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using (var stream = File.Create(filename))
        {
            encoder.Save(stream);
        }
    }
}