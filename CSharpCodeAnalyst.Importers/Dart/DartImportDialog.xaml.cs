using System.IO;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Importers.Resources;

namespace CSharpCodeAnalyst.Importers.Dart;

public partial class DartImportDialog : Window
{
    private readonly IUserNotification _ui;

    public DartImportDialog(DartImportDialogViewModel viewModel, IUserNotification ui)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        _ui = ui;
    }

    public DartImportDialogViewModel ViewModel { get; }

    private void BrowseProjectDirectory_Click(object sender, RoutedEventArgs e)
    {
        var initialDirectory = Directory.Exists(ViewModel.ProjectDirectory) ? ViewModel.ProjectDirectory : null;

        var path = _ui.ShowFolderBrowserDialog(Strings.ImportDart_SelectProjectDirectoryTitle, initialDirectory, this);
        if (path is not null)
        {
            ViewModel.ProjectDirectory = path;
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
