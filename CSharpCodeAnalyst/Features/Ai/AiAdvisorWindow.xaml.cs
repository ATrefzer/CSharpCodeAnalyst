using System.IO;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.Ai;

public partial class AiAdvisorWindow
{
    private static AiAdvisorWindow? _instance;

    private IUserNotification? _ui;

    private AiAdvisorWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Shows (or updates) the singleton advisor window with new content.
    ///     The window is owned by the main window so it closes together with the app.
    /// </summary>
    public static void ShowAdvice(string markdownText, IUserNotification ui)
    {
        if (_instance == null || !_instance.IsLoaded)
        {
            _instance = new AiAdvisorWindow();

            // Let it float behind the main window
            //_instance.Owner = Application.Current.MainWindow;

            _instance.Left = 0;
            _instance.Top = 0;

            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
        }
        else
        {
            _instance.Activate();
        }

        _instance._ui = ui;
        _instance.SetContent(markdownText);
    }

    private void SetContent(string markdownText)
    {
        MarkdownViewer.Markdown = markdownText;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var options = new FileDialogOptions
        {
            DefaultExt = ".md",
            FileName = Strings.AiAdvisorWindow_SaveDialog_DefaultFileName
        };

        var path = _ui?.ShowSaveFileDialog(Strings.AiAdvisorWindow_SaveDialog_Filter, Strings.AiAdvisorWindow_SaveDialog_Title, options);
        if (path is not null)
        {
            File.WriteAllText(path, MarkdownViewer.Markdown ?? string.Empty);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
