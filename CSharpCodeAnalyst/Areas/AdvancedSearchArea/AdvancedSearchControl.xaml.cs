using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CSharpCodeAnalyst.Areas.AdvancedSearchArea;

public partial class AdvancedSearchControl : UserControl
{
    public AdvancedSearchControl()
    {
        InitializeComponent();
    }
    
    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: not null } button
            && button.ContextMenu.Items[0] is MenuItem item)
        {
            button.ContextMenu.PlacementTarget = button;
            item.Tag = SearchDataGrid;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }
}