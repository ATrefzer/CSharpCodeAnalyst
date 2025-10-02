using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Common;

internal class WindowsMessageBox : IMessageBox
{
    public void ShowError(string message)
    {
        System.Windows.MessageBox.Show(message, Strings.Error_Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
}