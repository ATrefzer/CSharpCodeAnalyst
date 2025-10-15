using System.Text.RegularExpressions;
using System.Windows;
using CodeParser.Parser.Config;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Filter;

public partial class FilterDialog
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
        try
        {
            _filter.Initialize(FiltersTextBox.Text);
        }
        catch (RegexParseException)
        {
            MessageBox.Show(Strings.InvalidFilter_Message, Strings.InvalidFilter_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
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