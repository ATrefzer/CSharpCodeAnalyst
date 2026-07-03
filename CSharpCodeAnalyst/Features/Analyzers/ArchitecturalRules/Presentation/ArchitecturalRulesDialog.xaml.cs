using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using CSharpCodeAnalyst.Resources;
using Microsoft.Win32;

namespace CSharpCodeAnalyst.Features.Analyzers.ArchitecturalRules.Presentation;

public partial class ArchitecturalRulesDialog : INotifyPropertyChanged
{

    public ArchitecturalRulesDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public string RulesText
    {
        get;
        set
        {
            if (field == value) { return; }

            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    /// <summary>
    /// Callback invoked when the Validate button is clicked
    /// </summary>
    public Action<string>? OnValidateRequested { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
        // Invoke the validation callback without closing the dialog
        OnValidateRequested?.Invoke(RulesText);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadFileButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|Rules files (*.rules)|*.rules|All files (*.*)|*.*",
            Title = Strings.ArchitecturalRules_LoadDialog_Title
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                RulesText = File.ReadAllText(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Strings.ArchitecturalRules_LoadFileError_Message, ex.Message), Strings.Error_Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|Rules files (*.rules)|*.rules|All files (*.*)|*.*",
            Title = Strings.ArchitecturalRules_SaveDialog_Title
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(saveFileDialog.FileName, RulesText);
                MessageBox.Show(Strings.ArchitecturalRules_SaveFileSuccess_Message, Strings.Success_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Strings.ArchitecturalRules_SaveFileError_Message, ex.Message), Strings.Error_Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Strings.ArchitecturalRules_ClearConfirm_Message, Strings.ArchitecturalRules_ClearConfirm_Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            RulesText = string.Empty;
        }
    }
}