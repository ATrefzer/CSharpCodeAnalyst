using System.Windows.Media.Imaging;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Messages;

public static class CodeElementIconMapper
{
    private static readonly Dictionary<CodeElementType, BitmapImage> IconCache = new();

    static CodeElementIconMapper()
    {
        InitializeIcons();
    }

    public static BitmapImage GetIcon(CodeElementType elementType)
    {
        return IconCache.TryGetValue(elementType, out var icon) ? icon : IconCache[CodeElementType.Other];
    }

    private static void InitializeIcons()
    {
        IconCache[CodeElementType.Assembly] = LoadIcon("Assembly_16.png");
        IconCache[CodeElementType.Namespace] = LoadIcon("Namespace_16.png");
        IconCache[CodeElementType.Class] = LoadIcon("Class_16.png");
        IconCache[CodeElementType.Interface] = LoadIcon("Interface_16.png");
        IconCache[CodeElementType.Struct] = LoadIcon("Struct_16.png");
        IconCache[CodeElementType.Method] = LoadIcon("Method_16.png");
        IconCache[CodeElementType.Property] = LoadIcon("Property_16.png");
        IconCache[CodeElementType.Delegate] = LoadIcon("Delegate_16.png");
        IconCache[CodeElementType.Event] = LoadIcon("Event_16.png");
        IconCache[CodeElementType.Enum] = LoadIcon("Enum_16.png");
        IconCache[CodeElementType.Field] = LoadIcon("Field_16.png");
        IconCache[CodeElementType.Record] = LoadIcon("Record_16.png");
        IconCache[CodeElementType.Other] = LoadIcon("Other_16.png");
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