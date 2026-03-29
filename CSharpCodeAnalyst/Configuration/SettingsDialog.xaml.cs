using System.Text.RegularExpressions;
using System.Windows;
using CodeParser.Parser.Config;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Configuration;

public partial class SettingsDialog
{

    public SettingsDialog(ApplicationSettings applSettings, UserSettings userSettings)
    {
        InitializeComponent();
        UserSettings = userSettings.Clone();
        AppSettings = applSettings.Clone();
        LoadSettingsToUi();
    }

    public UserSettings UserSettings { get; }

    public ApplicationSettings AppSettings { get; private set; }

    private void LoadSettingsToUi()
    {
        AutoAddContainingTypeCheckBox.IsChecked = AppSettings.AutomaticallyAddContainingType;
        WarningLimitTextBox.Text = AppSettings.WarningCodeElementLimit.ToString();

        // Internal format to new line separated.
        ProjectExcludeFilterTextBox.Text = AppSettings.DefaultProjectExcludeFilter.Replace(";", Environment.NewLine);
        IncludeExternalCodeCheckBox.IsChecked = AppSettings.IncludeExternalCode;
        WarnIfFiltersActiveCheckBox.IsChecked = AppSettings.WarnIfFiltersActive;

        AiEndpointTextBox.Text = UserSettings.AiEndpoint;
        AiModelTextBox.Text = UserSettings.AiModel;
        if (AiCredentialStorage.HasApiKey())
        {
            AiApiKeyBox.Password = "placeholder";
        }
        else
        {
            AiApiKeyBox.Clear();
        }
    }

    private void SaveSettingsFromUi()
    {
        AppSettings.AutomaticallyAddContainingType = AutoAddContainingTypeCheckBox.IsChecked ?? true;
        AppSettings.IncludeExternalCode = IncludeExternalCodeCheckBox.IsChecked ?? true;
        AppSettings.WarnIfFiltersActive = WarnIfFiltersActiveCheckBox.IsChecked ?? true;

        if (int.TryParse(WarningLimitTextBox.Text, out var warningLimit) && warningLimit > 0)
        {
            AppSettings.WarningCodeElementLimit = warningLimit;
        }

        AppSettings.DefaultProjectExcludeFilter = ProjectExcludeFilterTextBox.Text;

        UserSettings.AiEndpoint = AiEndpointTextBox.Text.Trim();
        UserSettings.AiModel = AiModelTextBox.Text.Trim();

        // Only update the stored key if the user actually typed something new
        var typedKey = AiApiKeyBox.Password;
        if (typedKey != "placeholder" && typedKey.Length > 0)
        {
            AiCredentialStorage.SaveApiKey(typedKey);
        }
    }

    private void LoadDefaultSettings()
    {
        AppSettings = new ApplicationSettings();
        UserSettings.AiEndpoint = UserSettings.DefaultAiEndpoint;
        UserSettings.AiModel = UserSettings.DefaultAiModel;
        LoadSettingsToUi();
    }


    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filter = new ProjectExclusionRegExCollection();
            filter.Initialize(ProjectExcludeFilterTextBox.Text);
        }
        catch (RegexParseException)
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