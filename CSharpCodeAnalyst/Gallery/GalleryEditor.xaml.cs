using System.Windows;
using System.Windows.Controls;

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

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Single-click preview: when selection changes, trigger preview
        if (DataContext is GalleryEditorViewModel viewModel && viewModel.SelectedItem != null)
        {
            viewModel.PreviewSelectedItemCommand.Execute(viewModel.SelectedItem);
        }
    }
}