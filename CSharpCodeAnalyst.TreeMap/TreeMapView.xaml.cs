using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CSharpCodeAnalyst.TreeMap.Drawing;
using CSharpCodeAnalyst.TreeMap.Interfaces;
using CSharpCodeAnalyst.TreeMap.Tools;
using CSharpCodeAnalyst.TreeMap.TreeMap;

namespace CSharpCodeAnalyst.TreeMap
{
    /// <summary>
    /// Interaction logic for TreeMapView.xaml
    /// </summary>
    public sealed partial class TreeMapView : HierarchicalDataViewBase
    {
        public TreeMapView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            ToolsExtension.Instance.ToolCloseRequested += Instance_ToolCloseRequested;

            _popup.Placement = PlacementMode.Custom;
            _popup.CustomPopupPlacementCallback = PlaceTooltip;
        }

        /// <summary>
        ///     Keeps the tooltip clear of the mouse cursor. PlacementMode.Mouse flips the popup
        ///     above the cursor when it would cross the bottom of the screen work area; the popup
        ///     then covers the cursor, the canvas receives a MouseLeave (which closes the popup),
        ///     the next MouseMove reopens it, and so on - the tooltip flickers. All candidates
        ///     below keep a gap to the cursor position (the anchor rectangle), so none of them can
        ///     ever sit on top of it. WPF picks the first candidate that is fully visible on
        ///     screen.
        /// </summary>
        private static CustomPopupPlacement[] PlaceTooltip(Size popupSize, Size targetSize, Point offset)
        {
            const double gap = 14;
            return
            [
                // Below-right (preferred), above-right, below-left, above-left of the cursor.
                new CustomPopupPlacement(new Point(gap, gap), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(gap, -popupSize.Height - gap), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(-popupSize.Width - gap, gap), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(-popupSize.Width - gap, -popupSize.Height - gap), PopupPrimaryAxis.Horizontal)
            ];
        }

        private void Instance_ToolCloseRequested(object? sender, object e)
        {
            HideToolView();
        }

        protected override void ClosePopup()
        {
            _popup.IsOpen = false;
        }

        protected override IRenderer CreateRenderer()
        {
            return new SquarifiedTreeMapRenderer(BrushFactory);
        }

        protected override DrawingCanvas GetCanvas()
        {
            return _canvasOrImage;
        }

        protected override void InitPopup(IHierarchicalData hit)
        {
            _popupText.Text = hit.Description;

            // Anchor the popup to the current mouse position; PlaceTooltip decides on which
            // side of the anchor the tooltip actually opens.
            var canvas = GetCanvas();
            _popup.PlacementTarget = canvas;
            _popup.PlacementRectangle = new Rect(Mouse.GetPosition(canvas), new Size(1, 1));
            _popup.IsOpen = true;
        }
    }
}