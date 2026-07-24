using System.IO;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.Import;

public partial class DoxygenImportDialog : Window
{
    private readonly IUserNotification _ui;

    public DoxygenImportDialog(DoxygenImportDialogViewModel viewModel, IUserNotification ui)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        _ui = ui;
    }

    public DoxygenImportDialogViewModel ViewModel { get; }

    private void BrowseSourceDirectory_Click(object sender, RoutedEventArgs e)
    {
        var initialDirectory = Directory.Exists(ViewModel.SourceDirectory) ? ViewModel.SourceDirectory : null;

        var path = _ui.ShowFolderBrowserDialog(Strings.ImportDoxygen_SelectSourceDirectoryTitle, initialDirectory, this);
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
