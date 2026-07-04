using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CSharpCodeAnalyst.AnalyzerSdk.Wpf;

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
        var assemblyName = Assembly.GetCallingAssembly().GetName().Name;
        return LoadIcon(assemblyName, iconPath);
    }

    /// <summary>
    ///     Loads an icon embedded as a WPF "Resource" in the given assembly - use this for icons
    ///     shared across assemblies (e.g. SDK icons referenced by an analyzer plugin).
    /// </summary>
    public static ImageSource? LoadIcon(string? assemblyName, string iconPath)
    {
        try
        {
            var cacheKey = $"{assemblyName}|{iconPath}";

            if (IconCache.TryGetValue(cacheKey, out var icon))
            {
                return icon;
            }

            var bitmap = Load(assemblyName, iconPath);

            IconCache[cacheKey] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage Load(string? assemblyName, string iconPath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri($"pack://application:,,,/{assemblyName};component/{iconPath}");
        bitmap.DecodePixelWidth = 16;
        bitmap.DecodePixelHeight = 16;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}