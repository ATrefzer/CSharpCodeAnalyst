using System.Windows;
using System.Windows.Controls;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public partial class GraphSearchControl : UserControl
{
    public GraphSearchControl()
    {
        InitializeComponent();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is GraphSearchViewModel viewModel)
        {
            viewModel.ToggleSearchVisibility();

            // Focus search box when opening
            if (viewModel.IsSearchVisible)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    SearchTextBox.Focus();
                    SearchTextBox.SelectAll();
                });
            }
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is GraphSearchViewModel viewModel)
        {
            viewModel.ClearSearch();
            SearchTextBox.Focus();
        }
    }
}