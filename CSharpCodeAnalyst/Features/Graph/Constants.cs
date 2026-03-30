using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Features.Graph;

internal static class Constants
{
    internal const int FlagLineWidth = 3;
    internal const int SearchHighlightLineWidth = 2;
    internal const double DefaultLineWidth = 1;
    internal const int MouseHighlightLineWidth = 3;
    internal static int DoubleClickMilliseconds = 350;

    internal static Color FlagColor = Color.Red;
    internal static Color SearchHighlightColor = Color.Red;
    internal static Color DefaultLineColor = Color.Black;
    internal static readonly Color GrayColor = Color.LightGray;
    internal static readonly Color MouseHighlightColor = Color.Red;
}
