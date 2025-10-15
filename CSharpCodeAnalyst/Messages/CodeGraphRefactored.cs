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

internal class CodeElementsDeleted(CodeGraph codeGraph, string deletedElementId, string? parentId, HashSet<string> deletedIds) : CodeGraphRefactored(codeGraph)
{
    public string DeletedElementId { get; } = deletedElementId;
    public HashSet<string> DeletedIds { get; } = deletedIds;
    public string? ParentId { get; set; } = parentId;
}

internal class RelationshipsDeleted(CodeGraph codeGraph, List<Relationship> deleted) : CodeGraphRefactored(codeGraph)
{
    public List<Relationship> Deleted { get; } = deleted;

}

internal class CodeElementsMoved(CodeGraph codeGraph, string sourceId, string oldParentId, string newParentId) : CodeGraphRefactored(codeGraph)
{
    public string SourceId { get; } = sourceId;
    public string OldParentId { get; } = oldParentId;
    public string NewParentId { get; } = newParentId;
}