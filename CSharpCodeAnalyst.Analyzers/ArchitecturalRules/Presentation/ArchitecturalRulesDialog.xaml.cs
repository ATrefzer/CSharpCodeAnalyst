using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Analyzers.Resources;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

public partial class ArchitecturalRulesDialog : INotifyPropertyChanged
{

    public ArchitecturalRulesDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>Set by the owner (Analyzer) right after construction.</summary>
    public IUserNotification? UserNotification { get; set; }

    public string RulesText
    {
        get;
        set
        {
            if (field == value) { return; }

            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    /// <summary>
    ///     Summary of the last validation, shown inline in the dialog.
    /// </summary>
    public string StatusText
    {
        get;
        set
        {
            if (field == value) { return; }

            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    /// <summary>
    ///     Whether the last validation found violations. Controls the Accept-Baseline button.
    /// </summary>
    public bool HasViolations
    {
        get;
        set
        {
            if (field == value) { return; }

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Callback invoked when the Validate button is clicked
    /// </summary>
    public Action<string>? OnValidateRequested { get; set; }

    /// <summary>
    /// Callback invoked when the Accept-Baseline button is clicked
    /// </summary>
    public Action? OnAcceptBaselineRequested { get; set; }

    /// <summary>
    /// Callback invoked when the Remove-Unused-Rules button is clicked
    /// </summary>
    public Action? OnRemoveUnusedRulesRequested { get; set; }

    /// <summary>
    /// Callback invoked when the Generate-Rules button is clicked
    /// </summary>
    public Action? OnGenerateRulesRequested { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
        // Invoke the validation callback without closing the dialog
        OnValidateRequested?.Invoke(RulesText);
    }

    private void AcceptBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        OnAcceptBaselineRequested?.Invoke();
    }

    private void RemoveUnusedRulesButton_Click(object sender, RoutedEventArgs e)
    {
        OnRemoveUnusedRulesRequested?.Invoke();
    }

    private void GenerateRulesButton_Click(object sender, RoutedEventArgs e)
    {
        OnGenerateRulesRequested?.Invoke();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private const string RulesFileFilter = "Text files (*.txt)|*.txt|Rules files (*.rules)|*.rules|All files (*.*)|*.*";

    private void LoadFileButton_Click(object sender, RoutedEventArgs e)
    {
        var path = UserNotification?.ShowOpenFileDialog(RulesFileFilter, Strings.ArchitecturalRules_LoadDialog_Title,
            new FileDialogOptions { Owner = this });
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            RulesText = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Strings.ArchitecturalRules_LoadFileError_Message, ex.Message), Strings.Error_Title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        var path = UserNotification?.ShowSaveFileDialog(RulesFileFilter, Strings.ArchitecturalRules_SaveDialog_Title,
            new FileDialogOptions { Owner = this });
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, RulesText);
            MessageBox.Show(Strings.ArchitecturalRules_SaveFileSuccess_Message, Strings.Success_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Strings.ArchitecturalRules_SaveFileError_Message, ex.Message), Strings.Error_Title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Strings.ArchitecturalRules_ClearConfirm_Message, Strings.ArchitecturalRules_ClearConfirm_Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            RulesText = string.Empty;
        }
    }
}