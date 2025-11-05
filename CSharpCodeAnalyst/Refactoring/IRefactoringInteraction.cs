using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Refactoring;

internal interface IRefactoringInteraction
{
    CodeElementSpecs? AskUserForCodeElementSpecs(CodeElement parent, List<CodeElementType> validElementTypes, ICodeElementNaming naming);

    bool AskUserToProceed(string message);
}