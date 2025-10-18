using System.Windows.Media;

namespace CSharpCodeAnalyst;

public static class UiConstants
{

    static UiConstants()
    {
        ToolbarBackground = new SolidColorBrush(Color.FromArgb(14, 14, 14, 14));
        ToolbarBackground.Freeze();
    }

    public static Brush ToolbarBackground { get; }
}