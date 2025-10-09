using Contracts.Graph;

namespace CSharpCodeAnalyst.Messages;

/// <summary>
///     Base class for all refactoring related notifications.
/// </summary>
public abstract class CodeGraphRefactored(CodeGraph codeGraph)
{

    public CodeGraph Graph { get; set; } = codeGraph;
}

internal class CodeElementCreated(CodeGraph codeGraph, CodeElement newElement) : CodeGraphRefactored(codeGraph)
{

    public CodeElement NewElement { get; set; } = newElement;
}

internal class CodeElementsDeleted(CodeGraph codeGraph, CodeElement deletedElement, HashSet<string> deletedIds) : CodeGraphRefactored(codeGraph)
{

    public CodeElement
        DeletedElement { get; } = deletedElement;

    public HashSet<string> DeletedIds { get; } = deletedIds;
}

internal class CodeElementsMoved(CodeGraph codeGraph, string sourceId, string oldParentId, string newParentId) : CodeGraphRefactored(codeGraph)
{
    public string SourceId { get; } = sourceId;
    public string OldParentId { get; } = oldParentId;
    public string NewParentId { get; } = newParentId;
}