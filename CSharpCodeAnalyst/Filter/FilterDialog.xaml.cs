using System.Windows;

namespace CSharpCodeAnalyst.Filter;

public partial class FilterDialog : Window
{
    public FilterDialog(List<string> currentFilters)
    {
        InitializeComponent();
        FiltersTextBox.Text = string.Join(Environment.NewLine, currentFilters);
    }

    public List<string> Filters { get; private set; } = [];

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Filters = FiltersTextBox.Text
            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}