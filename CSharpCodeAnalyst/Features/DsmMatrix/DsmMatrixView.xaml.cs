using System.Windows.Controls;
using System.Windows.Input;
using CSharpCodeAnalyst.Shared.Tabs;

namespace CSharpCodeAnalyst.Features.DsmMatrix;

/// <summary>
///     Hosts DsmSuite's matrix view plus our own overlay toolbar. Its DataContext is the
///     <see cref="DsmTabViewModel" />: the hosted MatrixView is rebound onto its <c>Matrix</c> (DsmSuite's
///     own MainViewModel, see <see cref="DsmMatrixFactory" />), and the toolbar binds the tab's
///     <see cref="DsmTabViewModel.OpenFileCommand" />.
/// </summary>
public partial class DsmMatrixView : UserControl
{
    /// <summary>
    ///     Zoom range for ctrl+wheel. The lower bound is what lets a large matrix be shrunk down to the
    ///     window: illegible, but the shape (layering, cycle blocks near the diagonal) still reads. It is far
    ///     below DsmSuite's own 0.5, which only bounds their toolbar zoom commands — we set ZoomLevel
    ///     directly and clamp here instead.
    /// </summary>
    private const double MinZoom = 0.04;

    private const double MaxZoom = 4.0;
    private const double ZoomStep = 1.15;

    public DsmMatrixView()
    {
        InitializeComponent();
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    /// <summary>
    ///     Ctrl+wheel zooms the whole matrix. The scale itself is DsmSuite's: MatrixView already carries a
    ///     ScaleTransform bound to MatrixViewModel.ZoomLevel, as a LayoutTransform — so zooming out re-runs
    ///     layout at the larger logical size and genuinely fits more matrix into the window, rather than just
    ///     shrinking the viewport. Only the operation was missing, because it sits on the viewer's own
    ///     toolbar, which we do not host.
    /// </summary>
    /// <remarks>
    ///     Deliberately ctrl+wheel, not plain wheel: the cells live in a ScrollViewer, and plain wheel has to
    ///     stay with scrolling, which is what you need at a readable zoom level.
    /// </remarks>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (DataContext is not DsmTabViewModel { Matrix.ActiveMatrix: not null } tab)
        {
            return;
        }

        var matrix = tab.Matrix.ActiveMatrix;
        var factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        matrix.ZoomLevel = Math.Clamp(matrix.ZoomLevel * factor, MinZoom, MaxZoom);

        // Otherwise the ScrollViewer underneath scrolls as well.
        e.Handled = true;
    }
}
