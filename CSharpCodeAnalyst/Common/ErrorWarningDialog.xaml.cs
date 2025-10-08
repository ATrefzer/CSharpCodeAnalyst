using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace CSharpCodeAnalyst.Common;

public partial class ErrorWarningDialog : Window
{
    private ErrorWarningDialog(List<string> errors, List<string> warnings)
    {
        InitializeComponent();



        ErrorList.ItemsSource = errors;
        WarningList.ItemsSource = warnings;

        // Hide tabs
        if (errors.Count == 0)
        {
            Tabs.Items.Remove(ErrorTab);
        }

        if (warnings.Count == 0)
        {
            Tabs.Items.Remove(WarningTab);
        }

        if (Tabs.Items.Count > 0)
        {
            ((TabItem)Tabs.Items[0]).IsSelected = true;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    public static void Show(List<string>? errors, List<string>? warnings, Window? owner = null)
    {
        if (errors == null)
        {
            errors = [];
        }

        if (warnings == null)
        {
            warnings = [];
        }

        if (errors.Count == 0 && warnings.Count == 0)
        {
            // Nothing to show
            return;
        }

        var dialog = new ErrorWarningDialog(errors, warnings);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        
        dialog.ShowDialog();
    }
}