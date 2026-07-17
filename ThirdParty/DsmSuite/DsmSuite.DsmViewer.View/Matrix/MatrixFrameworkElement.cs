// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Media;

namespace DsmSuite.DsmViewer.View.Matrix
{
    public class MatrixFrameworkElement : FrameworkElement
    {
        private static readonly GlyphTypeface GlyphTypeface;
        private static readonly float PixelsPerDip;
        private static readonly RotateTransform TextTransform;
        private static readonly double FontSize = 14;

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: marks a shortened text, see <see cref="Ellipsize"/>. A
        /// single glyph, present in Segoe UI (10.26px at font size 14) — three periods would cost less
        /// (9.11px) but read as content rather than as a cut.
        /// </summary>
        private const string Ellipsis = "…";
        private static readonly GlyphInfo[] MGlyphInfoTable;
        private static readonly List<ushort> MGlyphIndexesList = new List<ushort>();
        private static readonly List<double> MAdvanceWidthsList = new List<double>();

        private struct GlyphInfo
        {
            public readonly ushort Index;
            public readonly double Width;

            public GlyphInfo(ushort glyphIndex, double width) : this()
            {
                Index = glyphIndex;
                Width = width;
            }
        }

        static MatrixFrameworkElement()
        {
            Typeface typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            typeface.TryGetGlyphTypeface(out GlyphTypeface);
            PixelsPerDip = 1.0f;

            MGlyphInfoTable = new GlyphInfo[char.MaxValue];
            foreach (var kvp in GlyphTypeface.CharacterToGlyphMap)
            {
                char c = (char)kvp.Key;
                var glyphIndex = kvp.Value;
                double width = GlyphTypeface.AdvanceWidths[glyphIndex] * FontSize;
                MGlyphInfoTable[c] = new GlyphInfo(glyphIndex, width);
            }

            TextTransform = new RotateTransform { Angle = 90 };
        }

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: the baseline offset, measured from the top of a box
        /// <paramref name="boxHeight"/> tall, that puts a line of digits in its vertical middle.
        /// </summary>
        /// <remarks>
        /// A glyph run is positioned by its baseline, and digits sit on the baseline and rise by the cap
        /// height, so their middle is half a cap height above it. Derived from the font rather than tuned
        /// by hand, so it survives a change of MatrixCellSize or of the font size.
        /// </remarks>
        protected static double CenteredTextBaseline(double boxHeight, double? fontSize = null)
        {
            double capHeight = GlyphTypeface.CapsHeight * (fontSize ?? FontSize);
            return (boxHeight + capHeight) / 2;
        }

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: <see cref="MGlyphInfoTable"/> caches every advance width at
        /// <see cref="FontSize"/>. Advance widths scale linearly with the size, so a caller that wants a
        /// different one gets it by scaling. Returns 1.0 for the standard size, i.e. for every caller that
        /// does not ask.
        /// </summary>
        private static double SizeScale(double? fontSize)
        {
            return fontSize.HasValue ? fontSize.Value / FontSize : 1.0;
        }

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: <paramref name="text"/> if it fits in
        /// <paramref name="maxWidth"/>, otherwise as much of it as fits followed by an ellipsis.
        /// </summary>
        /// <remarks>
        /// DrawText simply stops emitting glyphs once the width runs out, which passes a prefix off as the
        /// whole name — "CodeElementFactory" and "CodeElementFilter" both end up reading "CodeElement", with
        /// nothing to say that anything is missing. The ellipsis is what makes the cut visible. The result
        /// is guaranteed to fit, so DrawText's own clipping never fires on it.
        /// </remarks>
        protected string Ellipsize(string text, double maxWidth, double? fontSize = null)
        {
            if (MeasureText(text, fontSize) <= maxWidth)
            {
                return text;
            }

            double budget = maxWidth - MeasureText(Ellipsis, fontSize);
            if (budget <= 0)
            {
                // Not even the ellipsis fits: nothing legible can be drawn, and a lone dot on the wrong
                // side of the edge is worse than an empty cell.
                return string.Empty;
            }

            double scale = SizeScale(fontSize);
            double total = 0;
            int fitting = 0;
            foreach (char c in text)
            {
                double width = MGlyphInfoTable[c].Width * scale;
                if (total + width > budget)
                {
                    break;
                }

                total += width;
                fitting++;
            }

            return text.Substring(0, fitting) + Ellipsis;
        }

        protected void DrawRotatedText(DrawingContext dc, string text, Point location, SolidColorBrush color, double maxWidth)
        {
            Point rotatedLocation = new Point(-location.Y, -location.X);
            dc.PushTransform(TextTransform);
            DrawText(dc, text, rotatedLocation, color, maxWidth);
            dc.Pop();
        }

        /// <param name="fontSize">
        /// Added 2026-07 for CSharpCodeAnalyst: null draws at the standard <see cref="FontSize"/>, which is
        /// what every caller but the matrix cells wants. The cells need a smaller one to fit a four digit
        /// weight, see MatrixCellsView.
        /// </param>
        protected void DrawText(DrawingContext dc, string text, Point location, SolidColorBrush color, double maxWidth, double? fontSize = null)
        {
            if (text.Length > 0)
            {
                double totalWidth = 0;
                double scale = SizeScale(fontSize);

                MGlyphIndexesList.Clear();
                MAdvanceWidthsList.Clear();

                foreach (char c in text)
                {
                    if (totalWidth < maxWidth)
                    {
                        var info = MGlyphInfoTable[c];
                        double width = info.Width * scale;
                        MGlyphIndexesList.Add(info.Index);
                        MAdvanceWidthsList.Add(width);

                        totalWidth += width;
                    }
                }

                if (MGlyphIndexesList.Count > 0)
                {
                    GlyphRun glyphRun = new GlyphRun(GlyphTypeface, 0, false, fontSize ?? FontSize, PixelsPerDip,
                        MGlyphIndexesList.ToArray(), location, MAdvanceWidthsList.ToArray(),
                        null, null, null, null, null, null);

                    dc.DrawGlyphRun(color, glyphRun);
                }
            }
        }
        /// <param name="fontSize">
        /// Added 2026-07 for CSharpCodeAnalyst: has to match what the text is drawn at, see DrawText.
        /// </param>
        protected double MeasureText(string text, double? fontSize = null)
        {
            double totalWidth = 0;
            double scale = SizeScale(fontSize);

            foreach (char c in text)
            {
                var info = MGlyphInfoTable[c];
                totalWidth += info.Width * scale;
            }
            return totalWidth;
        }
    }
}
