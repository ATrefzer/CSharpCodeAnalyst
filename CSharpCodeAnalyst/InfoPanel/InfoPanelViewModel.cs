using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Help;
using CSharpCodeAnalyst.Resources;
using Prism.Commands;

namespace CSharpCodeAnalyst.InfoPanel;

internal class InfoPanelViewModel : INotifyPropertyChanged
{
    private bool _hide;

    private List<QuickInfo> _quickInfo = QuickInfoFactory.NoInfoProviderRegistered;

    public InfoPanelViewModel()
    {
        OpenSourceLocationCommand = new DelegateCommand<SourceLocation>(OpenSourceLocation);
        Hide(true);
    }

    public ICommand OpenSourceLocationCommand { get; }



    public List<QuickInfo> QuickInfo
    {
        get => _quickInfo;
        set
        {
            if (Equals(value, _quickInfo))
            {
                return;
            }

            _quickInfo = value;
            OnPropertyChanged();
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    private void OpenSourceLocation(SourceLocation? location)
    {
        if (location is null)
        {
            return;
        }

        var process = new Process();
        var startInfo = new ProcessStartInfo
        {
            FileName = "\"C:\\Program Files\\Notepad++\\notepad++.exe\"",
            Arguments = $"-n{location.Line} -c{location.Column} \"{location.File}\"",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };

        try
        {
            process.StartInfo = startInfo;
            process.Start();
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void HandleUpdateQuickInfo(QuickInfoUpdate quickInfoUpdate)
    {
        // May come from any view
        if (_hide)
        {
            // This can be very slow if updated even the help is not visible.
            return;
        }

        QuickInfo = quickInfoUpdate.QuickInfo;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Clear()
    {
        _quickInfo = QuickInfoFactory.NoInfoProviderRegistered;
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