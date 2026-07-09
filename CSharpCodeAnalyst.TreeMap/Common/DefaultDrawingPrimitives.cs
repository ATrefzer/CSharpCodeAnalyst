using System.Windows;
using System.Windows.Media;

namespace CSharpCodeAnalyst.TreeMap.Common
{
    public static class DefaultDrawingPrimitives
    {
        public static readonly SolidColorBrush HighlightBrush = Brushes.Yellow;
        public static readonly Pen BlackPen = new Pen(Brushes.Black, 1.0);
        public static readonly SolidColorBrush DefaultBrush = Brushes.LightGray;
        public static readonly Color DefaultColor = Colors.LightGray;
        public static readonly GradientBrush WhiteToRedGradient;

        static DefaultDrawingPrimitives()
        {
            // Sequential "Reds" ramp (ColorBrewer, 9 classes): warm near-white to deep red.
            // A plain two-stop gray/red gradient interpolates through desaturated, muddy
            // in-between tones; the intermediate stops keep the chroma up across the whole
            // range. Perceptually ordered and colorblind-safe.
            var stops = new GradientStopCollection
            {
                new GradientStop(Color.FromRgb(0xFF, 0xF5, 0xF0), 0.0),
                new GradientStop(Color.FromRgb(0xFE, 0xE0, 0xD2), 0.125),
                new GradientStop(Color.FromRgb(0xFC, 0xBB, 0xA1), 0.25),
                new GradientStop(Color.FromRgb(0xFC, 0x92, 0x72), 0.375),
                new GradientStop(Color.FromRgb(0xFB, 0x6A, 0x4A), 0.5),
                new GradientStop(Color.FromRgb(0xEF, 0x3B, 0x2C), 0.625),
                new GradientStop(Color.FromRgb(0xCB, 0x18, 0x1D), 0.75),
                new GradientStop(Color.FromRgb(0xA5, 0x0F, 0x15), 0.875),
                new GradientStop(Color.FromRgb(0x67, 0x00, 0x0D), 1.0)
            };
            stops.Freeze();

            WhiteToRedGradient = new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 1));
            WhiteToRedGradient.Freeze();
            BlackPen.Freeze();
        }
    }
}