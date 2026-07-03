namespace CSharpCodeAnalyst;

/// <summary>
///     Indices of the tabs in the two main <c>TabControl</c>s (see <c>MainWindow.xaml</c>),
///     gathered in one place. The values must match the order of the <c>TabItem</c>s in the
///     XAML; if the tab order changes, update only this file (and the XAML).
/// </summary>
internal static class TabIndices
{
    /// <summary>Left panel — <c>CodeStructureTab</c>, bound to <c>SelectedLeftTabIndex</c>.</summary>
    internal static class Left
    {
        public const int TreeView = 0;
        public const int AdvancedSearch = 1;
        public const int InfoPanel = 2;
    }

    /// <summary>
    ///     Right working area — <c>WorkingArea</c>, bound to <c>SelectedRightTabIndex</c>. Only the
    ///     fixed tabs are listed here; dynamic analyzer/partitions tabs are appended after them and
    ///     selected directly (by TabItem, not by index) - see <c>MainWindow.xaml.cs</c>.
    /// </summary>
    internal static class Right
    {
        public const int WebView = 0;
        public const int Cycles = 1;
        public const int Statistics = 2;
    }
}
