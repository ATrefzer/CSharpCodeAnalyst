using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Common;

class WindowsMessageBox : IMessageBox
{
    public void ShowError(string message)
    {
        System.Windows.MessageBox.Show(message, Strings.Error_Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
}