using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

public partial class ArchitecturalRulesDialog : INotifyPropertyChanged
{
    private string _rulesText = string.Empty;

    public ArchitecturalRulesDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public string RulesText
    {
        get => _rulesText;
        set
        {
            if (_rulesText == value) { return; }

            _rulesText = value;
            OnPropertyChanged();
        }
    }

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
            Title = "Load rules"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                RulesText = File.ReadAllText(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|Rules files (*.rules)|*.rules|All files (*.*)|*.*",
            Title = "Save rules"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(saveFileDialog.FileName, RulesText);
                MessageBox.Show("Rules saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to clear all rules?", "Confirm Clear",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            RulesText = string.Empty;
        }
    }
}