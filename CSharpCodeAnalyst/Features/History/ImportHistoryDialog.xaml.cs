using System.IO;
using System.Windows;
using CSharpCodeAnalyst.Resources;
using Microsoft.Win32;

namespace CSharpCodeAnalyst.Features.History;

public partial class ImportHistoryDialog : Window
{
    private string? _confirmedOverwritePath;

    public ImportHistoryDialog(ImportHistoryDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
    }

    public ImportHistoryDialogViewModel ViewModel { get; }

    private void BrowseRepository_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Strings.History_SelectRepositoryRootTitle
        };

        if (Directory.Exists(ViewModel.RepositoryPath))
        {
            dialog.InitialDirectory = ViewModel.RepositoryPath;
        }

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.RepositoryPath = dialog.FolderName;
        }
    }

    private void BrowseOutputFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.History_SelectOutputFileTitle,
            Filter = Strings.History_TextFilesFilter,
            DefaultExt = ".txt",
            OverwritePrompt = true
        };

        var currentDirectory = Path.GetDirectoryName(ViewModel.OutputFilePath);
        if (!string.IsNullOrEmpty(currentDirectory) && Directory.Exists(currentDirectory))
        {
            dialog.InitialDirectory = currentDirectory;
            dialog.FileName = Path.GetFileName(ViewModel.OutputFilePath);
        }

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.OutputFilePath = dialog.FileName;

            // SaveFileDialog already asked for overwrite confirmation.
            _confirmedOverwritePath = dialog.FileName;
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