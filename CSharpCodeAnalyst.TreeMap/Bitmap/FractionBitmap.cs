using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap.Bitmap;

public sealed class FractionBitmap
{
    public void Create(string filename, Dictionary<string, uint> workByDevelopers,
        IBrushFactory brushFactory, bool legend)
    {
        double allWork = workByDevelopers.Values.Sum(w => w);

        const int width = 200;
        const int height = 200;

        var remainingWidth = width;
        var remainingHeight = height;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var sorted = workByDevelopers.ToList().OrderByDescending(pair => pair.Value).ToList();

            var oneUnitOfWork = width * height / allWork;
            var x = 0;
            var y = 0;

            var vertical = true;
            var index = 0;

            // Background is white
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 2000, 2000));

            foreach (var developersWork in sorted)
            {
                var brush = brushFactory.GetBrush(developersWork.Key);

                if (legend)
                {
                    var legendY = index * 30;
                    var legendX = 250;

                    var typeface = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                    var formattedText = new FormattedText(
                        developersWork.Key,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        12,
                        Brushes.Black,
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                    dc.DrawText(formattedText, new Point(legendX + 25, legendY));
                    dc.DrawRectangle(brush, new Pen(Brushes.Black, 1), new Rect(legendX, legendY, 20, 20));
                }

                var workArea = developersWork.Value;
                var pixelArea = oneUnitOfWork * workArea;

                if (index == sorted.Count - 1)
                {
                    pixelArea = remainingWidth * remainingHeight;
                }

                if (vertical)
                {
                    var widthOfWork = (int)Math.Round(pixelArea / remainingHeight);
                    dc.DrawRectangle(brush, new Pen(Brushes.Black, 1), new Rect(x, y, widthOfWork, remainingHeight));
                    x += widthOfWork;
                    remainingWidth -= widthOfWork;
                }
                else
                {
                    var heightOfWork = (int)Math.Round(pixelArea / remainingWidth);
                    dc.DrawRectangle(brush, new Pen(Brushes.Black, 1), new Rect(x, y, remainingWidth, heightOfWork));
                    y += heightOfWork;
                    remainingHeight -= heightOfWork;
                }

                vertical = !vertical;
                index++;
            }

            dc.DrawRectangle(null, new Pen(Brushes.Black, 1), new Rect(0, 0, width, height));
        }

        // Render bit map and save
        var renderTarget = new RenderTargetBitmap(2000, 2000, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(visual);

        var trimmedBitmap = BitmapManipulation.TrimBitmap(renderTarget);
        SaveBitmap(trimmedBitmap, filename);
    }
    
    private void SaveBitmap(BitmapSource bitmap, string filename)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using (var stream = File.Create(filename))
        {
            encoder.Save(stream);
        }
    }
}