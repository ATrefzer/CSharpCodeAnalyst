using Contracts.Graph;

namespace CSharpCodeAnalyst.Areas.GraphArea.Filtering;

/// <summary>
///     Represents which CodeElement types and Relationship types should be hidden from the graph view.
/// </summary>
public class GraphHideFilter
{

    /// <summary>
    ///     Defines which CodeElement types can be hidden by the user.
    ///     Structural types (Assembly, Namespace, Class, etc.) are excluded because hiding them
    ///     would hide their entire subtrees.
    /// </summary>
    public static readonly HashSet<CodeElementType> HideableElementTypes =
    [
        CodeElementType.Method,
        CodeElementType.Property,
        CodeElementType.Field,
        CodeElementType.Event,
        CodeElementType.Enum,
        CodeElementType.Delegate
    ];

    /// <summary>
    ///     Defines which Relationship types can be hidden by the user.
    ///     Containment is excluded as it's fundamental to the graph structure.
    /// </summary>
    public static readonly HashSet<RelationshipType> HideableRelationshipTypes =
    [
        RelationshipType.Calls,
        RelationshipType.Creates,
        RelationshipType.Uses,
        RelationshipType.Inherits,
        RelationshipType.Implements,
        RelationshipType.Overrides,
        RelationshipType.UsesAttribute,
        RelationshipType.Invokes,
        RelationshipType.Handles
    ];

    /// <summary>
    ///     CodeElement types that should be hidden from the graph.
    /// </summary>
    public HashSet<CodeElementType> HiddenElementTypes { get; set; } = [];

    /// <summary>
    ///     Relationship types that should be hidden from the graph.
    /// </summary>
    public HashSet<RelationshipType> HiddenRelationshipTypes { get; set; } = [];

    /// <summary>
    ///     Determines if a code element should be hidden based on the filter.
    /// </summary>
    public bool ShouldHideElement(CodeElement element)
    {
        return HiddenElementTypes.Contains(element.ElementType);
    }

    /// <summary>
    ///     Determines if a relationship should be hidden based on the filter.
    /// </summary>
    public bool ShouldHideRelationship(Relationship relationship)
    {
        return HiddenRelationshipTypes.Contains(relationship.Type);
    }

    /// <summary>
    ///     Checks if any filters are active.
    /// </summary>
    public bool IsActive()
    {
        return HiddenElementTypes.Count > 0 || HiddenRelationshipTypes.Count > 0;
    }

    /// <summary>
    ///     Clears all filters.
    /// </summary>
    public void Clear()
    {
        HiddenElementTypes.Clear();
        HiddenRelationshipTypes.Clear();
    }

    /// <summary>
    ///     Creates a copy of this filter.
    /// </summary>
    public GraphHideFilter Clone()
    {
        return new GraphHideFilter
        {
            HiddenElementTypes = new HashSet<CodeElementType>(HiddenElementTypes),
            HiddenRelationshipTypes = new HashSet<RelationshipType>(HiddenRelationshipTypes)
        };
    }
}