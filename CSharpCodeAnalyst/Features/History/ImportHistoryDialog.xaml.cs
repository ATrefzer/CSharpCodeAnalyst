using System.IO;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.History;

public partial class ImportHistoryDialog : Window
{
    private readonly IUserNotification _ui;
    private string? _confirmedOverwritePath;

    public ImportHistoryDialog(ImportHistoryDialogViewModel viewModel, IUserNotification ui)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        _ui = ui;
    }

    public ImportHistoryDialogViewModel ViewModel { get; }

    private void BrowseRepository_Click(object sender, RoutedEventArgs e)
    {
        var initialDirectory = Directory.Exists(ViewModel.RepositoryPath) ? ViewModel.RepositoryPath : null;

        var path = _ui.ShowFolderBrowserDialog(Strings.History_SelectRepositoryRootTitle, initialDirectory, this);
        if (path is not null)
        {
            ViewModel.RepositoryPath = path;
        }
    }

    private void BrowseOutputFile_Click(object sender, RoutedEventArgs e)
    {
        var currentDirectory = Path.GetDirectoryName(ViewModel.OutputFilePath);
        var options = new FileDialogOptions
        {
            DefaultExt = ".txt",
            OverwritePrompt = true,
            Owner = this
        };

        if (!string.IsNullOrEmpty(currentDirectory) && Directory.Exists(currentDirectory))
        {
            options = options with
            {
                InitialDirectory = currentDirectory,
                FileName = Path.GetFileName(ViewModel.OutputFilePath)
            };
        }

        var path = _ui.ShowSaveFileDialog(Strings.History_TextFilesFilter, Strings.History_SelectOutputFileTitle, options);
        if (path is not null)
        {
            ViewModel.OutputFilePath = path;

            // SaveFileDialog already asked for overwrite confirmation.
            _confirmedOverwritePath = path;
        }
    }

    private void OutputFilePathTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ConfirmOverwriteIfNeeded();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanAccept)
        {
            return;
        }

        if (!ConfirmOverwriteIfNeeded())
        {
            OutputFilePathTextBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    ///     Asks the user for confirmation once per distinct path when the chosen output file already exists.
    /// </summary>
    /// <returns>False if the user declined to overwrite an existing file.</returns>
    private bool ConfirmOverwriteIfNeeded()
    {
        var path = ViewModel.OutputFilePath;

        if (!ViewModel.OutputFileAlreadyExists || path == _confirmedOverwritePath)
        {
            return true;
        }

        var result = MessageBox.Show(this,
            string.Format(Strings.History_FileAlreadyExists_Message, path),
            Strings.History_FileAlreadyExists_Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _confirmedOverwritePath = path;
            return true;
        }

        return false;
    }
}