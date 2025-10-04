using System.Windows;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Configuration;

public partial class SettingsDialog
{
    public SettingsDialog(ApplicationSettings settings)
    {
        InitializeComponent();
        Settings = CloneSettings(settings);
        LoadSettingsToUi();
    }

    public ApplicationSettings Settings { get; private set; }

    private void LoadSettingsToUi()
    {
        AutoAddContainingTypeCheckBox.IsChecked = Settings.AutomaticallyAddContainingType;
        WarningLimitTextBox.Text = Settings.WarningCodeElementLimit.ToString();
        ProjectExcludeFilterTextBox.Text = Settings.DefaultProjectExcludeFilter;
        IncludeExternalCodeCheckBox.IsChecked = Settings.IncludeExternalCode;
    }

    private void SaveSettingsFromUi()
    {
        Settings.AutomaticallyAddContainingType = AutoAddContainingTypeCheckBox.IsChecked ?? true;
        Settings.IncludeExternalCode = IncludeExternalCodeCheckBox.IsChecked ?? true;

        if (int.TryParse(WarningLimitTextBox.Text, out var warningLimit) && warningLimit > 0)
        {
            Settings.WarningCodeElementLimit = warningLimit;
        }

        Settings.DefaultProjectExcludeFilter = ProjectExcludeFilterTextBox.Text.Trim();
    }

    private void LoadDefaultSettings()
    {
        var defaults = new ApplicationSettings();
        Settings = defaults;
        LoadSettingsToUi();
    }

    private static ApplicationSettings CloneSettings(ApplicationSettings original)
    {
        return new ApplicationSettings
        {
            WarningCodeElementLimit = original.WarningCodeElementLimit,
            DefaultProjectExcludeFilter = original.DefaultProjectExcludeFilter,
            AutomaticallyAddContainingType = original.AutomaticallyAddContainingType,
            IncludeExternalCode = original.IncludeExternalCode
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(Strings.Settings_Save_Qustion, Strings.Settings_Save_Title,
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            LoadDefaultSettings();
        }
    }
}