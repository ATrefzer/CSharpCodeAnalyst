using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.Shared.Services;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Analyzers.EventRegistration.Presentation;

public class EventImbalanceViewModel : TableRow
{
    internal EventImbalanceViewModel(Result imbalance)
    {
        Description = imbalance.Handler.FullName;
        Locations = new ObservableCollection<SourceLocation>(imbalance.Locations);
        OpenSourceLocationCommand = new WpfCommand<SourceLocation>(OnOpenSourceLocation);
    }

    public ICommand OpenSourceLocationCommand { get; set; }

    public ObservableCollection<SourceLocation> Locations { get; set; }

    public string Description { get; }


    private static void OnOpenSourceLocation(SourceLocation? location)
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