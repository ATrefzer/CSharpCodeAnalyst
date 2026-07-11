using System.Windows;

namespace CSharpCodeAnalyst.Features.Graph;

public partial class AddChildrenDialog
{
    public AddChildrenDialog(AddChildrenDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
    }

    public AddChildrenDialogViewModel ViewModel { get; }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
