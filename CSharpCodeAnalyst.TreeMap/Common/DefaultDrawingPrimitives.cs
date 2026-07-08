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
        public static readonly GradientBrush RedToWhiteGradient;
        public static readonly GradientBrush WhiteToRedGradient;

        static DefaultDrawingPrimitives()
        {
            RedToWhiteGradient = new LinearGradientBrush(Colors.Red, Colors.White, new Point(0, 0), new Point(1, 1));
            RedToWhiteGradient.GradientStops.Add(new GradientStop(Colors.Red, 0.0));
            RedToWhiteGradient.GradientStops.Add(new GradientStop(Colors.White, 1.0));
            RedToWhiteGradient.Freeze();

            WhiteToRedGradient = new LinearGradientBrush(Colors.LightGray, Colors.DarkRed, new Point(0, 0), new Point(1, 1));
            WhiteToRedGradient.GradientStops.Add(new GradientStop(Colors.White, 0.0));
            WhiteToRedGradient.GradientStops.Add(new GradientStop(Colors.Red, 1.0));
            WhiteToRedGradient.Freeze();
            BlackPen.Freeze();
        }
    }
}