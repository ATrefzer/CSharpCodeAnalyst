using System.IO;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.Import;

public partial class ImportCppDialog : Window
{
    private readonly IUserNotification _ui;

    public ImportCppDialog(ImportCppDialogViewModel viewModel, IUserNotification ui)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        _ui = ui;
    }

    public ImportCppDialogViewModel ViewModel { get; }

    private void BrowseSourceDirectory_Click(object sender, RoutedEventArgs e)
    {
        var initialDirectory = Directory.Exists(ViewModel.SourceDirectory) ? ViewModel.SourceDirectory : null;

        var path = _ui.ShowFolderBrowserDialog(Strings.ImportCpp_SelectSourceDirectoryTitle, initialDirectory, this);
        if (path is not null)
        {
            ViewModel.SourceDirectory = path;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
