using System.ComponentModel;
using System.Windows.Input;

namespace CSharpCodeAnalyst.Shared.Tabs;

/// <summary>
///     Common shape for tabs held in MainViewModel.DynamicTabs. MainWindow's code-behind picks the
///     content DataTemplate based on the concrete implementation (DynamicTabViewModel for tabular
///     analyzer results, HierarchicalTabViewModel for tree-map style results).
/// </summary>
public interface ITabViewModel : INotifyPropertyChanged
{
    string Id { get; }

    string Title { get; }

    /// <summary>Set by the owner (MainViewModel) right after construction.</summary>
    ICommand? CloseCommand { get; set; }
}
