using System.Windows;

namespace CSharpCodeAnalyst.Gallery;

/// <summary>
///     Interaction logic for GalleryEditor.xaml
/// </summary>
public partial class GalleryEditor
{
    public GalleryEditor()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}