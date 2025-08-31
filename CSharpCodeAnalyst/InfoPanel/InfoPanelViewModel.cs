using System.ComponentModel;
using System.Runtime.CompilerServices;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Help;

namespace CSharpCodeAnalyst.InfoPanel;

internal class InfoPanelViewModel : INotifyPropertyChanged
{
    private bool _hide;
    private bool _isInfoPanelVisible;

    private List<QuickInfo> _quickInfo = QuickInfoFactory.NoInfoProviderRegistered;

    public InfoPanelViewModel(ApplicationSettings settings)
    {
        _isInfoPanelVisible = settings.DefaultShowQuickHelp;
    }

    public bool IsInfoPanelVisible
    {
        get => _isInfoPanelVisible;
        set
        {
            if (_isInfoPanelVisible == value)
            {
                return;
            }

            _isInfoPanelVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInfoPanelVisibleEffective));
        }
    }

    public bool IsInfoPanelVisibleEffective
    {
        get => IsInfoPanelVisible && !_hide;
    }


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

    public void HandleUpdateQuickInfo(QuickInfoUpdate quickInfoUpdate)
    {
        // May come from any view
        if (IsInfoPanelVisible is false)
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
        OnPropertyChanged(nameof(IsInfoPanelVisibleEffective));
    }

    /// <summary>
    ///     Hide the info panel temporarily for example when the wong page is shown..
    /// </summary>
    public void Hide(bool hide)
    {
        _hide = hide;
        OnPropertyChanged(nameof(IsInfoPanelVisibleEffective));
    }
}