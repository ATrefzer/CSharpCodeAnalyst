using System.Windows;

namespace CSharpCodeAnalyst.Ai;

public partial class AiAdvisorWindow
{
    private static AiAdvisorWindow? _instance;

    private AiAdvisorWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Shows (or updates) the singleton advisor window with new content.
    ///     The window is owned by the main window so it closes together with the app.
    /// </summary>
    public static void ShowAdvice(string markdownText)
    {
        if (_instance == null || !_instance.IsLoaded)
        {
            _instance = new AiAdvisorWindow();
            _instance.Owner = Application.Current.MainWindow;

            // Place next to the main window
            if (Application.Current.MainWindow != null)
            {
                _instance.Left = Application.Current.MainWindow.Left + Application.Current.MainWindow.Width + 10;
                _instance.Top = Application.Current.MainWindow.Top;
            }

            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
        }
        else
        {
            _instance.Activate();
        }

        _instance.SetContent(markdownText);
    }

    private void SetContent(string markdownText)
    {
        MarkdownViewer.Markdown = markdownText;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
