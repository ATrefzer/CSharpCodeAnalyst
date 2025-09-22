using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CodeParser.Analysis.EventRegistration;
using Contracts.Graph;
using CSharpCodeAnalyst.InfoPanel;
using CSharpCodeAnalyst.Resources;
using Prism.Commands;

namespace CSharpCodeAnalyst.Areas.ResultArea;

public class EventImbalanceViewModel : INotifyPropertyChanged
{
    private readonly CodeElement _event;
    private bool _isExpanded;

    public EventImbalanceViewModel(EventRegistrationImbalance analyzer)
    {
        _event = analyzer.Event;
        Description = analyzer.Handler.FullName;
        Locations = new ObservableCollection<SourceLocation>(analyzer.Locations);
        OpenSourceLocationCommand = new DelegateCommand<SourceLocation>(OnOpenSourceLocation);
        _isExpanded = false;
    }

    public ICommand OpenSourceLocationCommand { get; set; }

    public ObservableCollection<SourceLocation> Locations { get; set; }

    public string Description { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value == _isExpanded) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;

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

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}