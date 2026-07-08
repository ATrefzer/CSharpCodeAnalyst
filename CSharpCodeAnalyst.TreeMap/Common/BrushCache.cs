using System.Windows.Media;

namespace CSharpCodeAnalyst.TreeMap.Common
{
    public static class BrushCache
    {
        /// <summary>
        /// Store System.Windows.Media.SolidColorBrushes for Wpf application.
        /// </summary>
        private static readonly Dictionary<Color, SolidColorBrush> Cache;

        static BrushCache()
        {
            Cache = new Dictionary<Color, SolidColorBrush>();
        }

        public static SolidColorBrush GetBrush(Color color)
        {
            if (!Cache.TryGetValue(color, out var brush))
            {
                brush = CreateBrushFromColor(color);
                Cache.Add(color, brush);
            }

            return brush;
        }

        private static SolidColorBrush CreateBrushFromColor(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}