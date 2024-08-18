using System.Windows;
using CodeParser.Parser.Config;

namespace CSharpCodeAnalyst.Filter;

public partial class FilterDialog : Window
{
    private readonly ProjectExclusionRegExCollection _filter;

    public FilterDialog(ProjectExclusionRegExCollection filter)
    {
        InitializeComponent();
        _filter = filter;
        FiltersTextBox.Text = string.Join(Environment.NewLine, filter.Expressions);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var expressions = FiltersTextBox.Text
            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();

        if (expressions.Any(f => f.Contains(";")))
        {
            MessageBox.Show("Filters cannot contain semicolons (;).", "Invalid Filter", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _filter.Initialize(expressions);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}