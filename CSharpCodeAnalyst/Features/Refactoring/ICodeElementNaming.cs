using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.Refactoring;

public interface ICodeElementNaming
{
    bool IsValid(CodeElementType type, string name);

    string GetDefaultName(CodeElementType type);
}