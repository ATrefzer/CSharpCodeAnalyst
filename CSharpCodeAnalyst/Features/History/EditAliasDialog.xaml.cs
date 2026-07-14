using System.Windows;
using System.Windows.Controls;

namespace CSharpCodeAnalyst.Features.History;

public partial class EditAliasDialog : Window
{
    public EditAliasDialog(EditAliasDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Force the cell currently being edited to write back before the caller reads the mapping.
        AliasGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        AliasGrid.CommitEdit(DataGridEditingUnit.Row, true);

        DialogResult = true;
        Close();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        // Drop any in-progress cell edit so the reset values are not overwritten on cell commit.
        AliasGrid.CancelEdit(DataGridEditingUnit.Cell);
        AliasGrid.CancelEdit(DataGridEditingUnit.Row);

        ((EditAliasDialogViewModel)DataContext).ResetToDefaults();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is EditAliasDialogViewModel viewModel)
        {
            // Commit a pending grid edit first: refreshing the view during an open edit
            // transaction (user was editing an alias, then clicked into the search box) would throw.
            AliasGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            AliasGrid.CommitEdit(DataGridEditingUnit.Row, true);

            viewModel.SearchText = SearchTextBox.Text;
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        // Clearing the text raises TextChanged, which resets the filter and restores all rows.
        SearchTextBox.Clear();
        SearchTextBox.Focus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
