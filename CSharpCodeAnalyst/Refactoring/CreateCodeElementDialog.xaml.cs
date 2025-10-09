using System.Windows;

namespace CSharpCodeAnalyst.Refactoring;

public partial class CreateCodeElementDialog : Window
{
    public CreateCodeElementDialog(CreateCodeElementDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;

        // Focus the name textbox for immediate editing
        Loaded += (s, e) =>
        {
            ElementNameTextBox.Focus();
            ElementNameTextBox.SelectAll();
        };
    }

    public CreateCodeElementDialogViewModel ViewModel { get; }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsValid())
        {
            MessageBox.Show("Please enter a valid element name.", "Invalid Name",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}