using System.Text.RegularExpressions;
using System.Windows;
using CodeParser.Parser.Config;
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
        
        // Internal format to new line separated.
        ProjectExcludeFilterTextBox.Text = Settings.DefaultProjectExcludeFilter.Replace(";", Environment.NewLine);
        IncludeExternalCodeCheckBox.IsChecked = Settings.IncludeExternalCode;
        WarnIfFiltersActiveCheckBox.IsChecked = Settings.WarnIfFiltersActive;
    }

    private void SaveSettingsFromUi()
    {
        Settings.AutomaticallyAddContainingType = AutoAddContainingTypeCheckBox.IsChecked ?? true;
        Settings.IncludeExternalCode = IncludeExternalCodeCheckBox.IsChecked ?? true;
        Settings.WarnIfFiltersActive = WarnIfFiltersActiveCheckBox.IsChecked ?? true;

        if (int.TryParse(WarningLimitTextBox.Text, out var warningLimit) && warningLimit > 0)
        {
            Settings.WarningCodeElementLimit = warningLimit;
        }
        
        Settings.DefaultProjectExcludeFilter = ProjectExcludeFilterTextBox.Text;
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
            IncludeExternalCode = original.IncludeExternalCode,
            WarnIfFiltersActive = original.WarnIfFiltersActive
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
      
        try
        {
            // Verify
            var filter = new ProjectExclusionRegExCollection();
            filter.Initialize(ProjectExcludeFilterTextBox.Text);
        }
        catch (RegexParseException ex)
        {
            MessageBox.Show(Strings.InvalidFilter_Message, Strings.InvalidFilter_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
            return; 
        }
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