using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Exports;

/// <summary>
///     This class was taken from https://github.com/microsoft/automatic-graph-layout samples.
/// </summary>
public static class ImageWriter
{
    public static void SaveToBmp(FrameworkElement visual, string fileName)
    {
        var encoder = new BmpBitmapEncoder();
        SaveUsingEncoder(visual, fileName, encoder);
    }

    public static void SaveToPng(FrameworkElement visual, string fileName)
    {
        var encoder = new PngBitmapEncoder();
        SaveUsingEncoder(visual, fileName, encoder);
    }

    private static void SaveUsingEncoder(FrameworkElement? visual, string fileName, BitmapEncoder encoder)
    {
        if (visual is null)
        {
            return;
        }

        var bitmap = CreateBitmap(visual);

        // Crop transparent space
        var croppedBitmap = ImageCrop.CropTransparency(bitmap);
        croppedBitmap.Freeze();

        var frame = BitmapFrame.Create(croppedBitmap);
        encoder.Frames.Add(frame);

        using (var stream = File.Create(fileName))
        {
            encoder.Save(stream);
        }
    }

    private static BitmapSource? CreateBitmap(FrameworkElement? visual)
    {
        if (visual is null)
        {
            return null;
        }

        var (dpiX, dpiY, pixelWidth, pixelHeight) = CalculatePixels96Dpi(visual);

        var bitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpiX,
            dpiY,
            PixelFormats.Pbgra32);

        bitmap.Render(visual);
        return bitmap;
    }

    private static (double dpiX, double dpiY, int pixelWidth, int pixelHeight) CalculatePixelsScreenDpi(FrameworkElement visual)
    {
        // Get the DPI of the visual's presentation source. 96 is WPFs baseline
        var source = PresentationSource.FromVisual(visual);
        var dpiX = 96.0;
        var dpiY = 96.0;

        if (source != null)
        {
            // Scaling factors: 150% = 1.5 = 144 pixes/inch
            dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
        }

        // Calculate actual pixel dimensions based on DPI
        // Actual width returns DIP (device independent pixels) based on 96 DPI
        var scaleX = dpiX / 96.0;
        var scaleY = dpiY / 96.0;
        var pixelWidth = (int)Math.Ceiling(visual.ActualWidth * scaleX);
        var pixelHeight = (int)Math.Ceiling(visual.ActualHeight * scaleY);
        return (dpiX, dpiY, pixelWidth, pixelHeight);
    }

    private static (double dpiX, double dpiY, int pixelWidth, int pixelHeight) CalculatePixels96Dpi(FrameworkElement visual)
    {
        // WPF internally uses 96 DPI
        var pixelWidth = (int)Math.Ceiling(visual.ActualWidth);
        var pixelHeight = (int)Math.Ceiling(visual.ActualHeight);
        return (96.0, 96.0, pixelWidth, pixelHeight);
    }

    private static RenderTargetBitmap CreateBitmapWithBackground(FrameworkElement visual, Brush background)
    {
        var (dpiX, dpiY, pixelWidth, pixelHeight) = CalculatePixels96Dpi(visual);

        var bitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpiX,
            dpiY,
            PixelFormats.Pbgra32);

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // Pixel zurück in DIPs umrechnen für die Rect
            // double scaleX = dpiX / 96.0;
            // double scaleY = dpiY / 96.0;
            // double rectWidth = pixelWidth / scaleX;
            // double rectHeight = pixelHeight / scaleY;
            // Avoid black line at the bottom due to rounding errors.

            var rectWidth = visual.ActualWidth + 2;
            var rectHeight = visual.ActualHeight + 2;

            context.DrawRectangle(
                background,
                null,
                new Rect(0, 0, rectWidth, rectHeight));

            var visualBrush = new VisualBrush(visual);
            context.DrawRectangle(
                visualBrush,
                null,
                new Rect(0, 0, rectWidth, rectHeight));
        }

        bitmap.Render(drawingVisual);
        return bitmap;
    }



    public static void CopyToClipboard(FrameworkElement visual)
    {
        if (visual is null)
        {
            return;
        }

        // Transparent background is drawn in black in Paint, Paint .NET, DrawIo
        //var transparentBitmap = CreateBitmap(visual);
        //transparentBitmap.Freeze();
        //Clipboard.SetData(DataFormats.Bitmap, transparentBitmap);
        //dataObject.SetData(DataFormats.Bitmap, transparentBitmap);

        // Bitmap with white background. Otherwise, its drawn black in Paint
        var whiteBitmap = CreateBitmapWithBackground(visual, Brushes.White);
        var croppedBitmap = ImageCrop.CropWhiteSpace(whiteBitmap);
        croppedBitmap.Freeze();
        Clipboard.SetData(DataFormats.Bitmap, croppedBitmap);
    }
}