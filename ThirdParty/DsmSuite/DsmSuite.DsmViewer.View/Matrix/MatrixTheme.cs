// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.ViewModel.Matrix;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace DsmSuite.DsmViewer.View.Matrix
{
    public class MatrixTheme
    {
        private SolidColorBrush[] _brushes;
        private readonly FrameworkElement _frameworkElement;

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: the presence fill sits behind the six cell colours, which
        /// occupy four slots each.
        /// </summary>
        private const int PresenceBrushIndex = 24;

        public MatrixTheme(FrameworkElement frameworkElement)
        {
            _frameworkElement = frameworkElement;

            MatrixCellSize = (double)_frameworkElement.FindResource("MatrixCellSize");
            MatrixHeaderHeight = (double)_frameworkElement.FindResource("MatrixHeaderHeight");
            MatrixMetricsViewWidth = (double)_frameworkElement.FindResource("MatrixMetricsViewWidth");
            TextColor = (SolidColorBrush)_frameworkElement.FindResource("TextColor");
            CellWeightColor = (SolidColorBrush)_frameworkElement.FindResource("CellWeightColor");
            MatrixColorConsumer = (SolidColorBrush)_frameworkElement.FindResource("MatrixColorConsumer");
            MatrixColorProvider = (SolidColorBrush)_frameworkElement.FindResource("MatrixColorProvider");
            MatrixColorMatch = (SolidColorBrush)_frameworkElement.FindResource("MatrixColorMatch");
            MatrixColorBookmark = (SolidColorBrush)_frameworkElement.FindResource("MatrixColorBookmark");
            MatrixColorCycle = (SolidColorBrush)_frameworkElement.FindResource("MatrixColorCycle");

            LeftArrow = (string)_frameworkElement.FindResource("LeftArrowIcon");
            RightArrow = (string)_frameworkElement.FindResource("RightArrowIcon");
            UpArrow = (string)_frameworkElement.FindResource("UpArrowIcon");
            DownArrow = (string)_frameworkElement.FindResource("DownArrowIcon");

            RightArrowFormattedText = new FormattedText(RightArrow,
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                10,
                TextColor);

            DownArrowFormattedText = new FormattedText(DownArrow,
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                10,
                TextColor);
        }

        public double ScrollBarWidth => 20.0;
        public double SpacingWidth => 2.0;
        public double IndicatorBarWidth => 5.0;
        public double MatrixCellSize { get; }
        public double MatrixHeaderHeight { get; }
        public double MatrixMetricsViewWidth { get; }
        public SolidColorBrush TextColor { get; }
        public SolidColorBrush CellWeightColor { get; }
        public SolidColorBrush MatrixColorConsumer { get; }
        public SolidColorBrush MatrixColorProvider { get; }
        public SolidColorBrush MatrixColorMatch { get; }
        public SolidColorBrush MatrixColorBookmark { get; }
        public SolidColorBrush MatrixColorCycle { get; }
        public string LeftArrow { get; }
        public string RightArrow { get; }
        public string UpArrow { get; }
        public string DownArrow { get; }
        public FormattedText RightArrowFormattedText { get; }
        public FormattedText DownArrowFormattedText { get; }

        public SolidColorBrush GetBackground(MatrixColor color, bool isHovered, bool isSelected)
        {
            UpdateBrushes();
            return _brushes[GetBrushIndex(color, isHovered, isSelected)];
        }

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: fill for a populated cell whose weight cannot be drawn any
        /// more. It is <c>TextColor</c>, the colour the number itself had, so the cell reads as that number
        /// collapsed into a block rather than as a new kind of marking.
        /// </summary>
        /// <remarks>
        /// The cell colours behind it are the normal ones - there is no separate palette for small zoom
        /// levels. A weakened depth ramp was tried and dropped: it bought the fill contrast it did not
        /// need (this is a whole cell, not a 10px glyph, and it clears 4:1 against even the deepest level)
        /// and cost the nested blocks their separation, which is the only structure left to read once the
        /// numbers are gone.
        /// </remarks>
        public SolidColorBrush GetPresenceBackground(bool isHovered, bool isSelected)
        {
            UpdateBrushes();
            return _brushes[PresenceBrushIndex + HighlightOffset(isHovered, isSelected)];
        }

        private static int GetBrushIndex(MatrixColor color, bool isHovered, bool isSelected)
        {
            int colorIndex;
            switch (color)
            {
                case MatrixColor.Background:
                    colorIndex = 0;
                    break;
                case MatrixColor.Color1:
                    colorIndex = 4;
                    break;
                case MatrixColor.Color2:
                    colorIndex = 8;
                    break;
                case MatrixColor.Color3:
                    colorIndex = 12;
                    break;
                case MatrixColor.Color4:
                    colorIndex = 16;
                    break;
                case MatrixColor.Cycle:
                    colorIndex = 20;
                    break;
                default:
                    colorIndex = 0;
                    break;
            }

            return colorIndex + HighlightOffset(isHovered, isSelected);
        }

        private static int HighlightOffset(bool isHovered, bool isSelected)
        {
            int offset = 0;

            if (isHovered)
            {
                offset += 1;
            }

            if (isSelected)
            {
                offset += 2;
            }

            return offset;
        }

        private void UpdateBrushes()
        {
            if (_brushes == null)
            {
                // Changed 2026-07 for CSharpCodeAnalyst: four more slots for the presence fill, see
                // GetPresenceBackground.
                _brushes = new SolidColorBrush[PresenceBrushIndex + 4];

                SolidColorBrush brushBackground = (SolidColorBrush)_frameworkElement.FindResource("MatrixColorBackground");
                SolidColorBrush brush1 = (SolidColorBrush)_frameworkElement.FindResource("MatrixColor1");
                SolidColorBrush brush2 = (SolidColorBrush)_frameworkElement.FindResource("MatrixColor2");
                SolidColorBrush brush3 = (SolidColorBrush)_frameworkElement.FindResource("MatrixColor3");
                SolidColorBrush brush4 = (SolidColorBrush)_frameworkElement.FindResource("MatrixColor4");
                SolidColorBrush brushCycle = (SolidColorBrush)_frameworkElement.FindResource("MatrixColorCycle");
                // Changed 2026-07 for CSharpCodeAnalyst: these hold channel steps, not factors, see
                // GetHighlightBrush. The resource keys keep their upstream names - renaming them would mean
                // editing all three of their theme dictionaries for nothing.
                double highlightStepsHovered = (double)_frameworkElement.FindResource("HighlightFactorHovered");
                double highlightStepsSelected = (double)_frameworkElement.FindResource("HighlightFactorSelected");

                SetBrush(0, brushBackground, highlightStepsHovered, highlightStepsSelected);
                SetBrush(1, brush1, highlightStepsHovered, highlightStepsSelected);
                SetBrush(2, brush2, highlightStepsHovered, highlightStepsSelected);
                SetBrush(3, brush3, highlightStepsHovered, highlightStepsSelected);
                SetBrush(4, brush4, highlightStepsHovered, highlightStepsSelected);
                SetBrush(5, brushCycle, highlightStepsHovered, highlightStepsSelected);

                // Added 2026-07 for CSharpCodeAnalyst: the presence fill, see GetPresenceBackground.
                SetBrush(6, TextColor, highlightStepsHovered, highlightStepsSelected);
            }
        }

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: freezes a brush before it goes into the cache.
        /// </summary>
        /// <remarks>
        /// Every use of a mutable Freezable in a DrawingContext costs WPF a change subscription, and
        /// MatrixCellsView.OnRender issues one DrawRectangle per cell - so a brush that lands on the bulk
        /// of the matrix is used matrixSize squared times. The brushes from the resource dictionaries are
        /// all declared po:Freeze="True"; the ones derived here were not.
        /// <para>
        /// As the code stands that is latent rather than active: the cells that make up the bulk are
        /// painted with the resource brushes themselves, and only the hovered and selected row and column
        /// reach a derived one. It was not latent while a weakened depth ramp was being tried for small
        /// zoom levels - every cell got a derived brush then, and the application stopped responding. That
        /// ramp was dropped for unrelated reasons (see DsmMatrixTheme.xaml), so freezing is no longer what
        /// keeps the matrix drawing. Keep it anyway: it is free, and it makes deriving a brush that covers
        /// many cells a safe thing to do rather than a trap.
        /// </para>
        /// </remarks>
        private static SolidColorBrush Frozen(SolidColorBrush brush)
        {
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        private void SetBrush(int colorIndex, SolidColorBrush brush, double highlightStepsHovered, double highlightStepsSelected)
        {
            int index = colorIndex * 4;
            _brushes[index] = brush;
            _brushes[index + 1] = GetHighlightBrush(brush, highlightStepsHovered);
            _brushes[index + 2] = GetHighlightBrush(brush, highlightStepsSelected);
            // Changed 2026-07 for CSharpCodeAnalyst: the two amounts add up now that they are steps rather
            // than factors. This is the cell where the hovered row meets the selected column, so it is
            // meant to be the darkest of the three.
            _brushes[index + 3] = GetHighlightBrush(brush, highlightStepsHovered + highlightStepsSelected);
        }

        /// <summary>
        /// Changed 2026-07 for CSharpCodeAnalyst: darkens by a fixed number of channel steps instead of
        /// multiplying the colour, and freezes the result (see <see cref="Frozen"/>). The amount is what
        /// HighlightFactorHovered / HighlightFactorSelected now hold.
        /// </summary>
        /// <remarks>
        /// A multiplication is proportional to the colour it is applied to, so it gave the crosshair a
        /// different strength on every cell it crossed - and both ends of our depth ramp came off badly.
        /// On the deepest level (#748C9E) the hover factor of 1.1 moved the channels by 11 to 15 steps,
        /// barely visible. On the empty cell (#E4E7EA), which is most of the matrix, green and blue hit
        /// the 255 ceiling at once, so the cell did not get brighter, it got *warmer* - a hue shift where
        /// contrast was intended. Raising the factor does not help: it clips the light end sooner while
        /// still under-moving the dark one.
        /// <para>
        /// A fixed step is the same visible change everywhere, which is what a pointer aid wants. It
        /// darkens rather than lightens because the matrix is light: there is far more room below the
        /// cells than above them. The one place it does little is the near-black presence fill, which is
        /// already the strongest mark on the matrix and does not need the crosshair to be found.
        /// </para>
        /// <para>
        /// Note for anyone re-enabling DsmSuite's Dark theme: its factors (1.2 / 1.4) lighten dark cells,
        /// which is right for a dark matrix and wrong under this implementation. It would need the step
        /// applied upwards there.
        /// </para>
        /// </remarks>
        public static SolidColorBrush GetHighlightBrush(SolidColorBrush color, double darkenSteps)
        {
            Color c = color.Color;
            return Frozen(new SolidColorBrush(Color.FromRgb(
                Darken(c.R, darkenSteps),
                Darken(c.G, darkenSteps),
                Darken(c.B, darkenSteps))));
        }

        private static byte Darken(byte channel, double steps)
        {
            return (byte)Math.Max(0.0, channel - steps);
        }
    }
}
