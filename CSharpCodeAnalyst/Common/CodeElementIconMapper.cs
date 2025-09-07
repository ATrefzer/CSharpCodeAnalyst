using System.Windows.Media.Imaging;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Common;

public static class CodeElementIconMapper
{
    private static readonly Dictionary<CodeElementType, BitmapImage> _iconCache = new();

    static CodeElementIconMapper()
    {
        InitializeIcons();
    }

    public static BitmapImage GetIcon(CodeElementType elementType)
    {
        return _iconCache.TryGetValue(elementType, out var icon) ? icon : _iconCache[CodeElementType.Other];
    }

    private static void InitializeIcons()
    {
        _iconCache[CodeElementType.Assembly] = LoadIcon("Assembly_16.png");
        _iconCache[CodeElementType.Namespace] = LoadIcon("Namespace_16.png");
        _iconCache[CodeElementType.Class] = LoadIcon("Class_16.png");
        _iconCache[CodeElementType.Interface] = LoadIcon("Interface_16.png");
        _iconCache[CodeElementType.Struct] = LoadIcon("Struct_16.png");
        _iconCache[CodeElementType.Method] = LoadIcon("Method_16.png");
        _iconCache[CodeElementType.Property] = LoadIcon("Property_16.png");
        _iconCache[CodeElementType.Delegate] = LoadIcon("Delegate_16.png");
        _iconCache[CodeElementType.Event] = LoadIcon("Event_16.png");
        _iconCache[CodeElementType.Enum] = LoadIcon("Enum_16.png");
        _iconCache[CodeElementType.Field] = LoadIcon("Field_16.png");
        _iconCache[CodeElementType.Record] = LoadIcon("Record_16.png");
        _iconCache[CodeElementType.Other] = LoadIcon("Other_16.png");
    }

    private static BitmapImage LoadIcon(string fileName)
    {
        var uri = new Uri($"pack://application:,,,/Resources/{fileName}");
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = uri;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze(); // Make it thread-safe and improve performance
        return bitmap;
    }
}