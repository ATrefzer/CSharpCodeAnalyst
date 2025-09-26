using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CSharpCodeAnalyst.Wpf
{
    internal static class IconLoader
    {
        private static readonly Dictionary<string, ImageSource?> IconCache = new();
        public static ImageSource? LoadIcon(string iconPath)
        {
            try
            {
                if (IconCache.TryGetValue(iconPath, out var icon))
                {
                    return icon;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"pack://application:,,,/{iconPath}");
                bitmap.DecodePixelWidth = 16;
                bitmap.DecodePixelHeight = 16;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                IconCache[iconPath] = bitmap;
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
