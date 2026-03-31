using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Features.Help;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Services;
using CSharpCodeAnalyst.Shared.Wpf;

namespace CSharpCodeAnalyst.Features.Info;

internal sealed class InfoPanelViewModel : INotifyPropertyChanged
{
    private bool _hide;

    public InfoPanelViewModel()
    {
        OpenSourceLocationCommand = new WpfCommand<SourceLocation>(OpenSourceLocation);
        Hide(true);
    }

    public ICommand OpenSourceLocationCommand { get; }


    public List<QuickInfo> QuickInfo
    {
        get;
        private set
        {
            if (Equals(value, field))
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    } = QuickInfoFactory.NoInfoProviderRegistered;


    public event PropertyChangedEventHandler? PropertyChanged;

    private static void OpenSourceLocation(SourceLocation? location)
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

    public void HandleUpdateQuickInfo(QuickInfoUpdateRequest quickInfoUpdateRequest)
    {
        // May come from any view
        if (_hide)
        {
            // This can be very slow if updated even the help is not visible.
            return;
        }

        QuickInfo = quickInfoUpdateRequest.QuickInfo;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void ClearQuickInfo()
    {
        QuickInfo = QuickInfoFactory.DefaultInfo;
        OnPropertyChanged(nameof(QuickInfo));
    }

    /// <summary>
    ///     Hide the info panel temporarily when not visible.
    ///     This does not waste computation if the info panel is hidden.
    /// </summary>
    public void Hide(bool hide)
    {
        _hide = hide;
    }
}