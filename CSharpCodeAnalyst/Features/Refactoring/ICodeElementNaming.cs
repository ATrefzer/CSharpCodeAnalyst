using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Refactoring;

public interface ICodeElementNaming
{
    bool IsValid(CodeElementType type, string name);

    string GetDefaultName(CodeElementType type);
}