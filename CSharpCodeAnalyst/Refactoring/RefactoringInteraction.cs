using System.Windows;
using Contracts.Graph;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Refactoring;

public class RefactoringInteraction : IRefactoringInteraction
{
    public CodeElementSpecs? AskUserForCodeElementSpecs(CodeElement? parent, List<CodeElementType> validElementTypes, ICodeElementNaming naming)
    {
        var viewModel = new CreateCodeElementDialogViewModel(parent, validElementTypes, naming);
        var dialog = new CreateCodeElementDialog(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        var result = dialog.ShowDialog();
        if (result == true)
        {
            return new CodeElementSpecs(viewModel.SelectedElementType, viewModel.ElementName);
        }

        return null;
    }

    public bool AskUserToProceed(string message)
    {
        return MessageBox.Show(message, Strings.Proceed_Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
    }



}