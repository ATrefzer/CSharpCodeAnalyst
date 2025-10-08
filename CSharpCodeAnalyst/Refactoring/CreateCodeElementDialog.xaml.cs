using System.Windows;
using Contracts.Graph;

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

    public CodeElement? CreatedElement { get; private set; }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        CreatedElement = ViewModel.CreateElement();

        if (CreatedElement == null)
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
        CreatedElement = null;
        DialogResult = false;
        Close();
    }
}
