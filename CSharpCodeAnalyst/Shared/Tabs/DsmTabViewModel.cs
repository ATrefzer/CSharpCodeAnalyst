using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

// Both this application and DsmSuite have a MainViewModel.
using DsmMainViewModel = DsmSuite.DsmViewer.ViewModel.Main.MainViewModel;

namespace CSharpCodeAnalyst.Shared.Tabs;

/// <summary>
///     A tab created on demand for the dependency structure matrix, keyed by <see cref="Id" /> so that
///     rebuilding the matrix updates the existing tab in place instead of creating a duplicate. Owned by an
///     <see cref="System.Collections.ObjectModel.ObservableCollection{T}" /> on MainViewModel;
///     MainWindow's code-behind projects that collection onto the working-area TabControl.
/// </summary>
/// <remarks>
///     Unlike the other tabs this one carries a whole view model rather than data: the matrix is DsmSuite's
///     own MainViewModel, and their MatrixView expects to find it as its DataContext.
/// </remarks>
public sealed class DsmTabViewModel(string id, string title, DsmMainViewModel matrix) : ITabViewModel
{
    public DsmMainViewModel Matrix
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    } = matrix;

    public string Id { get; } = id;

    public string Title
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    } = title;

    /// <summary>Set by the owner (MainViewModel) right after construction.</summary>
    public ICommand? CloseCommand { get; set; }

    /// <summary>
    ///     Opens a DSM or DSI file into this tab, replacing <see cref="Matrix" />. Set by the owner
    ///     (MainViewModel) right after construction, since the load needs the file dialog and the loading
    ///     indicator that live there. Bound from the toolbar in DsmMatrixView.
    /// </summary>
    public ICommand? OpenFileCommand { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
