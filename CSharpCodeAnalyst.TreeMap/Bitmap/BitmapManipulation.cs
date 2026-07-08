using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CSharpCodeAnalyst.TreeMap.Bitmap;

internal static class BitmapManipulation
{
    /// <summary>
    ///     Trims a bitmap by removing transparent pixels from the edges.
    ///     WPF version of the original Insight GDI+ implementation.
    /// </summary>
    public static BitmapSource TrimBitmap(BitmapSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        // Convert to Format32bppArgb if necessary
        var formattedSource = source.Format != PixelFormats.Bgra32
            ? new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0)
            : source;

        var width = formattedSource.PixelWidth;
        var height = formattedSource.PixelHeight;
        var stride = (width * formattedSource.Format.BitsPerPixel + 7) / 8;
        var buffer = new byte[height * stride];

        formattedSource.CopyPixels(buffer, stride, 0);

        int xMin = int.MaxValue,
            xMax = int.MinValue,
            yMin = int.MaxValue,
            yMax = int.MinValue;

        var foundPixel = false;

        // Find xMin left to right
        for (var x = 0; x < width; x++)
        {
            var stop = false;
            for (var y = 0; y < height; y++)
            {
                var alpha = buffer[y * stride + 4 * x + 3];
                if (alpha != 0)
                {
                    xMin = x;
                    stop = true;
                    foundPixel = true;
                    break;
                }
            }

            if (stop)
            {
                break;
            }
        }

        // Image is empty...
        if (!foundPixel)
        {
            return null;
        }

        // Find yMin top to bottom
        for (var y = 0; y < height; y++)
        {
            var stop = false;
            for (var x = xMin; x < width; x++)
            {
                var alpha = buffer[y * stride + 4 * x + 3];
                if (alpha != 0)
                {
                    yMin = y;
                    stop = true;
                    break;
                }
            }

            if (stop)
            {
                break;
            }
        }

        // Find xMax right to left
        for (var x = width - 1; x >= xMin; x--)
        {
            var stop = false;
            for (var y = yMin; y < height; y++)
            {
                var alpha = buffer[y * stride + 4 * x + 3];
                if (alpha != 0)
                {
                    xMax = x;
                    stop = true;
                    break;
                }
            }

            if (stop)
            {
                break;
            }
        }

        // Find yMax bottom to top
        for (var y = height - 1; y >= yMin; y--)
        {
            var stop = false;
            for (var x = xMin; x <= xMax; x++)
            {
                var alpha = buffer[y * stride + 4 * x + 3];
                if (alpha != 0)
                {
                    yMax = y;
                    stop = true;
                    break;
                }
            }

            if (stop)
            {
                break;
            }
        }

        var srcRect = new Int32Rect(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        return new CroppedBitmap(formattedSource, srcRect);
    }

    /// <summary>
    ///     Alternative: Trims a Bitmap based on a background color.
    /// </summary>
    public static BitmapSource TrimBitmap(BitmapSource source, Color backgroundColor)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var formattedSource = source.Format != PixelFormats.Bgra32
            ? new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0)
            : source;

        var width = formattedSource.PixelWidth;
        var height = formattedSource.PixelHeight;
        var stride = (width * formattedSource.Format.BitsPerPixel + 7) / 8;
        var buffer = new byte[height * stride];

        formattedSource.CopyPixels(buffer, stride, 0);

        int xMin = int.MaxValue,
            xMax = int.MinValue,
            yMin = int.MaxValue,
            yMax = int.MinValue;

        var foundPixel = false;
        var bgR = backgroundColor.R;
        var bgG = backgroundColor.G;
        var bgB = backgroundColor.B;
        var bgA = backgroundColor.A;

        // Find xMin
        for (var x = 0; x < width; x++)
        {
            var stop = false;
            for (var y = 0; y < height; y++)
            {
                var index = y * stride + 4 * x;
                var alpha = buffer[index + 3];

                // Prüfe ob Pixel nicht der Hintergrundfarbe entspricht
                if (alpha != 0 &&
                    (buffer[index] != bgB || buffer[index + 1] != bgG ||
                     buffer[index + 2] != bgR || buffer[index + 3] != bgA))
                {
                    xMin = x;
                    stop = true;
                    foundPixel = true;
                    break;
                }
            }

            if (stop)
            {
                break;
            }
        }

        if (!foundPixel)
        {
            return null;
        }

        // Find yMin
        for (var y = 0; y < height; y++)
        {
            var stop = false;
            for (var x = xMin; x < width; x++)
            {
                var index = y * stride + 4 * x;
                var alpha = buffer[index + 3];
                if (alpha != 0 &&
                    (buffer[index] != bgB || buffer[index + 1] != bgG ||
                     buffer[index + 2] != bgR || buffer[index + 3] != bgA))
                {
                    yMin = y;
                    stop = true;
                    break;
                }
            }

            if (stop)
            {
                break;
            }
        }

        // Find xMax
        for (var x = width - 1; x >= xMin; x--)
        {
            var stop = false;
            for (var y = yMin; y < height; y++)
            {
                var index = y * stride + 4 * x;
                var alpha = buffer[index + 3];
                if (alpha != 0 &&
                    (buffer[index] != bgB || buffer[index + 1] != bgG ||
                     buffer[index + 2] != bgR || buffer[index + 3] != bgA))
                {
                    xMax = x;
                    stop = true;
                    break;
                }
            }

            if (stop)
            {
                break;
            }
        }

        // Find yMax
        for (var y = height - 1; y >= yMin; y--)
        {
            var stop = false;
            for (var x = xMin; x <= xMax; x++)
            {
                var index = y * stride + 4 * x;
                var alpha = buffer[index + 3];
                if (alpha != 0 &&
                    (buffer[index] != bgB || buffer[index + 1] != bgG ||
                     buffer[index + 2] != bgR || buffer[index + 3] != bgA))
                {
                    yMax = y;
                    stop = true;
                    break;
                }
            }

            if (stop)
            {
                break;
            }
        }

        var srcRect = new Int32Rect(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        return new CroppedBitmap(formattedSource, srcRect);
    }
}