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

        using var stream = File.Create(fileName);
        encoder.Save(stream);
    }

    /// <summary>
    /// Note: The dpi in the bitmap is just metadata.
    /// We could let WPF render the visual to a 10000x10000 pixel bitmap to get a very high resolution.
    /// </summary>
    private static BitmapSource CreateBitmap(FrameworkElement visual)
    {
        var (dpiX, dpiY, pixelWidth, pixelHeight) = CalculatePixelDimension(visual);

        var bitmap = new RenderTargetBitmap(
            // Physical pixels
            pixelWidth, pixelHeight,
            // Metadata
            dpiX, dpiY,
            PixelFormats.Pbgra32);

        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>
    ///     Returns physical pixels the visual occupies on the screen.
    ///     Keep physical pixels and DPI metadata in sync to get the least problems.
    ///     visual.ActualWidth returns DIP (device independent pixels) based on 96 DPI
    /// </summary>
    private static (double dpiX, double dpiY, int pixelWidth, int pixelHeight) CalculatePixelDimension(FrameworkElement visual)
    {
        // Get the DPI of the visual's presentation source. WPF renders to this resolution. WPF baseline is 96 DPI.
        var source = PresentationSource.FromVisual(visual);
        var dpiX = 96.0;
        var dpiY = 96.0;

        if (source?.CompositionTarget != null)
        {
            // Scaling factors: 150% = 1.5 = 144 pixes/inch
            dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
        }

        var scaleX = dpiX / 96.0;
        var scaleY = dpiY / 96.0;
        var pixelWidth = (int)Math.Ceiling(visual.ActualWidth * scaleX);
        var pixelHeight = (int)Math.Ceiling(visual.ActualHeight * scaleY);
        return (dpiX, dpiY, pixelWidth, pixelHeight);
    }

    private static RenderTargetBitmap CreateBitmapWithBackground(FrameworkElement visual, Brush background)
    {
        var (dpiX, dpiY, pixelWidth, pixelHeight) = CalculatePixelDimension(visual);

        var bitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpiX,
            dpiY,
            PixelFormats.Pbgra32);

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // Convert physical pixels back to DIP
            var scaleX = dpiX / 96.0;
            var scaleY = dpiY / 96.0;
            var rectWidth = pixelWidth / scaleX;
            var rectHeight = pixelHeight / scaleY;

            // Draw white rectangle
            context.DrawRectangle(
                background,
                null,
                new Rect(0, 0, rectWidth, rectHeight));

            // Draw visual over it
            var visualBrush = new VisualBrush(visual);
            context.DrawRectangle(
                visualBrush,
                null,
                new Rect(0, 0, rectWidth, rectHeight));
        }

        bitmap.Render(drawingVisual);
        return bitmap;
    }

    public static void CopyToClipboard(FrameworkElement? visual)
    {
        if (visual is null)
        {
            return;
        }
        
        // Bitmap with white background. Otherwise, its drawn black in Paint
        var whiteBitmap = CreateBitmapWithBackground(visual, Brushes.White);
        var croppedBitmap = ImageCrop.CropWhiteSpace(whiteBitmap);
        croppedBitmap.Freeze();
        Clipboard.SetData(DataFormats.Bitmap, croppedBitmap);
    }
}