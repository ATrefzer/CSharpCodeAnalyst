using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CSharpCodeAnalyst.Shared.Wpf;

public static class IconLoader
{
    private static readonly Dictionary<string, ImageSource?> IconCache = new();

    /// <summary>
    ///     Loads an icon embedded as a WPF "Resource" in the calling assembly (e.g. the host or an
    ///     analyzer plugin), not necessarily the entry assembly - so each assembly can ship its own
    ///     icons without depending on another assembly's resources.
    /// </summary>
    public static ImageSource? LoadIcon(string iconPath)
    {
        try
        {
            var assemblyName = Assembly.GetCallingAssembly().GetName().Name;
            var cacheKey = $"{assemblyName}|{iconPath}";

            if (IconCache.TryGetValue(cacheKey, out var icon))
            {
                return icon;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/{assemblyName};component/{iconPath}");
            bitmap.DecodePixelWidth = 16;
            bitmap.DecodePixelHeight = 16;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            IconCache[cacheKey] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}