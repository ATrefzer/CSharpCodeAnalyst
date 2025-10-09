using Contracts.Graph;

namespace CSharpCodeAnalyst.Messages;

/// <summary>
///     Base class for all refactoring related notications.
/// </summary>
public abstract class CodeGraphRefactored
{
    protected CodeGraphRefactored(CodeGraph codeGraph)
    {
        Graph = codeGraph;
    }

    public CodeGraph Graph { get; set; }
}

internal class CodeElementCreated : CodeGraphRefactored
{
    public CodeElementCreated(CodeGraph codeGraph, CodeElement newElement) : base(codeGraph)
    {
        NewElement = newElement;
    }

    public CodeElement NewElement { get; set; }
}

internal class CodeElementsDeleted : CodeGraphRefactored
{

    public CodeElementsDeleted(CodeGraph codeGraph, CodeElement deletedElement, HashSet<string> deletedIds) : base(codeGraph)
    {
        DeletedElement = deletedElement;
        DeletedIds = deletedIds;
    }

    public CodeElement DeletedElement { get; }
    public HashSet<string> DeletedIds { get; }
}