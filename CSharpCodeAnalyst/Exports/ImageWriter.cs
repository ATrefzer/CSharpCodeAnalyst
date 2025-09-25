using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Exports;

/// <summary>
///     This class was taken from https://github.com/microsoft/automatic-graph-layout samples.
/// </summary>
public class ImageWriter
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

    public static void SaveUsingEncoder(FrameworkElement? visual, string fileName, BitmapEncoder encoder)
    {
        if (visual is null)
        {
            return;
        }

        var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96,
            PixelFormats.Pbgra32); // todo: seems wrong - might produce huge images
        bitmap.Render(visual);
        var frame = BitmapFrame.Create(bitmap);
        encoder.Frames.Add(frame);

        using (var stream = File.Create(fileName))
        {
            encoder.Save(stream);
        }
    }

    private void SaveToPng(Graph graph)
    {
        // Needs the Gdi package.
        //var renderer = new Microsoft.Msagl.GraphViewerGdi.GraphRenderer(graph);
        //renderer.CalculateLayout();
        //int width = 50;
        //Bitmap bitmap = new Bitmap(width, (int)(graph.Height *
        //                                        (width / graph.Width)), PixelFormat.Format32bppPArgb);
        //renderer.Render(bitmap);
        //bitmap.Save("test.png");
    }
}