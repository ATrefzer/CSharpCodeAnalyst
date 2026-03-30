using System.IO;
using System.Windows;
using System.Windows.Input;
using CSharpCodeAnalyst.Resources;
using Microsoft.Win32;

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

        _instance.SetContent(markdownText);
    }

    private void SetContent(string markdownText)
    {
        MarkdownViewer.Markdown = markdownText;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.AiAdvisorWindow_SaveDialog_Title,
            Filter = Strings.AiAdvisorWindow_SaveDialog_Filter,
            DefaultExt = ".md",
            FileName = Strings.AiAdvisorWindow_SaveDialog_DefaultFileName
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, MarkdownViewer.Markdown ?? string.Empty);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
