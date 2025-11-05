using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Messages;

/// <summary>
///     Base class for all refactoring related notifications.
/// </summary>
public abstract class CodeGraphRefactored(CodeGraph.Graph.CodeGraph codeGraph)
{

    public CodeGraph.Graph.CodeGraph Graph { get; set; } = codeGraph;
}

internal class CodeElementCreated(CodeGraph.Graph.CodeGraph codeGraph, CodeElement newElement) : CodeGraphRefactored(codeGraph)
{

    public CodeElement NewElement { get; set; } = newElement;
}

internal class CodeElementsDeleted(CodeGraph.Graph.CodeGraph codeGraph, string deletedElementId, string? parentId, HashSet<string> deletedIds) : CodeGraphRefactored(codeGraph)
{
    public string DeletedElementId { get; } = deletedElementId;
    public HashSet<string> DeletedIds { get; } = deletedIds;
    public string? ParentId { get; set; } = parentId;
}

internal class RelationshipsDeleted(CodeGraph.Graph.CodeGraph codeGraph, List<Relationship> deleted) : CodeGraphRefactored(codeGraph)
{
    public List<Relationship> Deleted { get; } = deleted;
}

internal class CodeElementsMoved(CodeGraph.Graph.CodeGraph codeGraph, HashSet<string> sourceIds, string newParentId) : CodeGraphRefactored(codeGraph)
{
    public HashSet<string> SourceIds { get; } = sourceIds;
    public string NewParentId { get; } = newParentId;
}