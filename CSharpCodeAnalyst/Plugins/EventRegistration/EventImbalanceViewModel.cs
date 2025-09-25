using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Areas.InfoArea;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Table;
using Prism.Commands;

namespace CSharpCodeAnalyst.Plugins.EventRegistration;

public class EventImbalanceViewModel : TableRow
{
    private readonly CodeElement _event;

    internal EventImbalanceViewModel(Result imbalance)
    {
        _event = imbalance.Event;
        Description = imbalance.Handler.FullName;
        Locations = new ObservableCollection<SourceLocation>(imbalance.Locations);
        OpenSourceLocationCommand = new DelegateCommand<SourceLocation>(OnOpenSourceLocation);
    }

    public ICommand OpenSourceLocationCommand { get; set; }

    public ObservableCollection<SourceLocation> Locations { get; set; }

    public string Description { get; }


    private void OnOpenSourceLocation(SourceLocation? location)
    {
        if (location is null)
        {
            return;
        }

        try
        {
            // Create a new instance to find newly open studio instance.
            var fileOpener = new FileOpener();
            fileOpener.TryOpenFile(location.File, location.Line, location.Column);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}