using System.Windows;
using System.Windows.Media.Imaging;

namespace CSharpCodeAnalyst.Wpf;

public static class ImageCrop
{
    /// <summary>
    ///     Cuts white edges from the image
    /// </summary>
    public static BitmapSource CropWhiteSpace(BitmapSource source, byte threshold = 250)
    {
        return CropInternal(source, IsCropPixel);

        bool IsCropPixel(byte[] pixels, int index)
        {
            return IsWhitePixel(pixels, index, threshold);
        }
    }

    public static BitmapSource CropTransparency(BitmapSource source, byte alphaThreshold = 10)
    {
        return CropInternal(source, IsCropPixel);

        bool IsCropPixel(byte[] pixels, int index)
        {
            return IsTransparentPixel(pixels, index, alphaThreshold);
        }
    }

    private static BitmapSource CropInternal(BitmapSource source, Func<byte[], int, bool> isCropPixel)
    {
        // Read pixel data
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4; // 4 bytes per pixel (BGRA)
        var pixels = new byte[height * stride];

        source.CopyPixels(pixels, stride, 0);


        // Scan all four edges
        var top = FindTop(pixels, width, height, isCropPixel);
        var bottom = FindBottom(pixels, width, height, isCropPixel);
        var left = FindLeft(pixels, width, height, isCropPixel);
        var right = FindRight(pixels, width, height, isCropPixel);

        // If everything is white
        if (top > bottom || left > right)
        {
            return source;
        }

        // Bitmap with new dimensions
        var newWidth = right - left + 1;
        var newHeight = bottom - top + 1;

        return new CroppedBitmap(source, new Int32Rect(left, top, newWidth, newHeight));
    }

    private static bool IsWhitePixel(byte[] pixels, int index, byte threshold)
    {
        // BGRA format: index+0=Blue, index+1=Green, index+2=Red, index+3=Alpha
        return pixels[index] >= threshold && // B
               pixels[index + 1] >= threshold && // G
               pixels[index + 2] >= threshold; // R
    }

    private static bool IsTransparentPixel(byte[] pixels, int index, byte alphaThreshold)
    {
        // BGRA format: index+3 = Alpha
        // Pixel is transparent when Alpha <= thresh dem Threshold alphaThreshold
        return pixels[index + 3] <= alphaThreshold;
    }

    private static int FindTop(byte[] pixels, int width, int height, Func<byte[], int, bool> isCropPixel)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                if (!isCropPixel(pixels, index))
                    return y;
            }
        }

        return height;
    }

    private static int FindBottom(byte[] pixels, int width, int height, Func<byte[], int, bool> isCropPixel)
    {
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                if (!isCropPixel(pixels, index))
                    return y;
            }
        }

        return -1;
    }

    private static int FindLeft(byte[] pixels, int width, int height, Func<byte[], int, bool> isCropPixel)
    {
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var index = (y * width + x) * 4;
                if (!isCropPixel(pixels, index))
                    return x;
            }
        }

        return width;
    }

    private static int FindRight(byte[] pixels, int width, int height, Func<byte[], int, bool> isCropPixel)
    {
        for (var x = width - 1; x >= 0; x--)
        {
            for (var y = 0; y < height; y++)
            {
                var index = (y * width + x) * 4;
                if (!isCropPixel(pixels, index))
                    return x;
            }
        }

        return -1;
    }
}