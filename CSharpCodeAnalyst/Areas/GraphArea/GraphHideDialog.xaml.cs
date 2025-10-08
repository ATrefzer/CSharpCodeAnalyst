using System.Windows;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public partial class GraphHideDialog : Window
{
    public GraphHideDialog(GraphHideDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
    }

    public GraphHideDialogViewModel ViewModel { get; }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Apply();
        DialogResult = true;
        Close();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Reset();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
