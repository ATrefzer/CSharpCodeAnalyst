using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Areas.AdvancedSearchArea;

public partial class AdvancedSearchControl : UserControl
{
    public AdvancedSearchControl()
    {
        InitializeComponent();
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: not null } button)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void SearchDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (sender is DataGrid { DataContext: AdvancedSearchViewModel viewModel })
            {
                e.Handled = true;
                viewModel.SelectAllCommand.Execute(null);
            }
        }
    }

    private void SearchDataGrid_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Update the refactoring movement parent
        if (SearchDataGrid.DataContext is AdvancedSearchViewModel vm)
        {
            var parent = vm.GetRefactoringNewMoveParent();
            MenuRefactoringMove.Header = string.IsNullOrEmpty(parent) ? Strings.Refactor_MoveSelectedCodeElements : string.Format(Strings.Refactor_MoveSelectedCodeElementTo, parent);
        }
    }
}