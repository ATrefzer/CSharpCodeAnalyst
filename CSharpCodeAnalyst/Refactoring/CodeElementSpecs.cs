using Contracts.Graph;

namespace CSharpCodeAnalyst.Refactoring;

public class CodeElementSpecs(CodeElementType elementType, string name)
{
    public CodeElementType ElementType { get; } = elementType;
    public string Name { get; } = name.Trim();
}