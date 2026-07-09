using System.Windows.Media.Imaging;

namespace CSharpCodeAnalyst.Features.ImageViewer;

public sealed partial class ImageViewer
{
    public ImageViewer()
    {
        InitializeComponent();
    }

    public void SetImage(string path)
    {
        _image.Source = new BitmapImage(new Uri(path));
    }

    public void SetImage(BitmapSource bitmapSource)
    {
        _image.Source = bitmapSource;
    }
}