using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst;

public class Constants
{
    public const double TreeMinWidthCollapsed = 24;
    public const double TreeMinWidthExpanded = 400;
    public const int FlagLineWidth = 3;
    public const int SearchHighlightLineWidth = 2;
    public const double DefaultLineWidth = 1;

    public static Color FlagColor = Color.Red;
    public static Color SearchHighlightColor = Color.Red;
    public static Color DefaultLineColor = Color.Black;

    public static int DoubleClickMilliseconds = 350;
}