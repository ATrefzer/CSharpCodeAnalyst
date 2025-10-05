using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        var frame = BitmapFrame.Create(bitmap);
        encoder.Frames.Add(frame);

        using (var stream = File.Create(fileName))
        {
            encoder.Save(stream);
        }
    }

    private static BitmapSource CreateBitmap(FrameworkElement visual)
    {
        if (visual is null)
        {
            return null;
        }

        var (dpiX, dpiY, pixelWidth, pixelHeight) = CalculatePixels(visual);

        var bitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpiX,
            dpiY,
            PixelFormats.Pbgra32);

        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>
    ///     DIP (Device Independent Pixel) = length unit (like cm or inches)
    ///     DPI (Dots Per Inch) = resolution (pixels per inch = resolution of your monitor)
    ///     96 DPI monitor (100% scaling):
    ///     -> 800 DIPs = 800 physical pixels
    ///     144 DPI monitor(150% scaling):
    ///     -> 800 DIPs = 1200 physical pixels
    ///     192 DPI monitor(200% scaling):
    ///     -> 800 DIPs = 1600 physical pixels
    /// </summary>
    private static (double dpiX, double dpiY, int pixelWidth, int pixelHeight) CalculatePixels(FrameworkElement visual)
    {
        // Get the DPI of the visual's presentation source. 96 is WPFs baseline
        var source = PresentationSource.FromVisual(visual);
        double dpiX = 96.0;
        double dpiY = 96.0;

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
        int pixelWidth = (int)Math.Ceiling(visual.ActualWidth * scaleX);
        int pixelHeight = (int)Math.Ceiling(visual.ActualHeight * scaleY);
        return (dpiX, dpiY, pixelWidth, pixelHeight);
    }

    private static RenderTargetBitmap CreateBitmapWithBackground(FrameworkElement visual, Brush background)
    {
        var (dpiX, dpiY, pixelWidth, pixelHeight) = CalculatePixels(visual);

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
            double scaleX = dpiX / 96.0;
            double scaleY = dpiY / 96.0;
            double rectWidth = pixelWidth / scaleX;
            double rectHeight = pixelHeight / scaleY;

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

        // Drawn in black in Paint
        //var transparentBitmap = CreateBitmap(visual);
        //transparentBitmap.Freeze();
        //Clipboard.SetData(DataFormats.Bitmap, transparentBitmap);
        //dataObject.SetData(DataFormats.Bitmap, transparentBitmap);

        // Bitmap with white background. Otherwise, its drawn black in Paint
        var whiteBitmap = CreateBitmapWithBackground(visual, Brushes.White);
        var croppedBitmap = CropWhitespace(whiteBitmap, threshold: 250);
        croppedBitmap.Freeze();
        Clipboard.SetData(DataFormats.Bitmap, croppedBitmap);
    }

    /// <summary>
    /// Cuts white edges from the image
    /// </summary>
    public static BitmapSource CropWhitespace(BitmapSource source, byte threshold = 250)
    {
        // Read pixel data
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4; // 4 bytes per pixel (BGRA)
        byte[] pixels = new byte[height * stride];

        source.CopyPixels(pixels, stride, 0);

        // Scan all four edges
        int top = FindTop(pixels, width, height, threshold);
        int bottom = FindBottom(pixels, width, height, threshold);
        int left = FindLeft(pixels, width, height, threshold);
        int right = FindRight(pixels, width, height, threshold);

        // If everything is white
        if (top > bottom || left > right)
        {
            return source;
        }

        // Bitmap with new dimensions
        int newWidth = right - left + 1;
        int newHeight = bottom - top + 1;

        var cropped = new CroppedBitmap(source, new Int32Rect(left, top, newWidth, newHeight));

        return cropped;
    }

    private static bool IsWhitePixel(byte[] pixels, int index, byte threshold)
    {
        // BGRA format: index+0=Blue, index+1=Green, index+2=Red, index+3=Alpha
        return pixels[index] >= threshold &&     // B
               pixels[index + 1] >= threshold && // G
               pixels[index + 2] >= threshold;   // R
    }

    private static int FindTop(byte[] pixels, int width, int height, byte threshold)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                if (!IsWhitePixel(pixels, index, threshold))
                    return y;
            }
        }
        return height;
    }

    private static int FindBottom(byte[] pixels, int width, int height, byte threshold)
    {
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                if (!IsWhitePixel(pixels, index, threshold))
                    return y;
            }
        }
        return -1;
    }

    private static int FindLeft(byte[] pixels, int width, int height, byte threshold)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index = (y * width + x) * 4;
                if (!IsWhitePixel(pixels, index, threshold))
                    return x;
            }
        }
        return width;
    }

    private static int FindRight(byte[] pixels, int width, int height, byte threshold)
    {
        for (int x = width - 1; x >= 0; x--)
        {
            for (int y = 0; y < height; y++)
            {
                int index = (y * width + x) * 4;
                if (!IsWhitePixel(pixels, index, threshold))
                    return x;
            }
        }
        return -1;
    }
}