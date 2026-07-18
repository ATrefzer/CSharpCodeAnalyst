using CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;
using CSharpCodeAnalyst.CodeGraph.Graph;
using DsmSuite.DsmViewer.Application.Sorting;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace CSharpCodeAnalyst.Features.DsmMatrix;

/// <summary>
///     Populates a DsmSuite <see cref="IDsmModel" /> from a <see cref="CodeGraph.Graph.CodeGraph" />, so the
///     embedded matrix view can show the current solution without any file round trip.
/// </summary>
/// <remarks>
///     The topology comes from <see cref="TypeGraph" />: one vertex per internal type, dependencies lifted to
///     the containing type and deduplicated, external types and self edges dropped. The hierarchy above the
///     types (namespaces, assemblies) is taken from the code graph's parent chain rather than reconstructed
///     from dotted names, which is what DsmSuite's own DSI import has to do for lack of parent information.
/// </remarks>
public sealed class CodeGraphToDsmModelBuilder
{
    private const string RelationType = "Dependency";

    private readonly CodeGraph.Graph.CodeGraph _codeGraph;

    /// <summary>Maps a code element id to the DSM element created for it.</summary>
    private readonly Dictionary<string, IDsmElement> _dsmElementsByCodeElementId = [];

    private readonly IDsmModel _dsmModel;

    /// <summary>Namespaces left out of the model, see <see cref="FindPassThroughNamespaces" />.</summary>
    private HashSet<string> _passThroughNamespaces = [];

    public CodeGraphToDsmModelBuilder(IDsmModel dsmModel, CodeGraph.Graph.CodeGraph codeGraph)
    {
        _dsmModel = dsmModel;
        _codeGraph = codeGraph;
    }

    /// <summary>
    ///     Fills the model. Returns the number of types added, so the caller can tell an empty result from a
    ///     populated one without querying the model.
    /// </summary>
    public int Build()
    {
        _dsmModel.Clear();
        _dsmElementsByCodeElementId.Clear();

        var typeGraph = TypeGraph.Build(_codeGraph);
        _passThroughNamespaces = FindPassThroughNamespaces();

        foreach (var typeId in typeGraph.Vertices)
        {
            AddWithAncestors(typeId);
        }

        AddRelations(typeGraph);

        Partition(_dsmModel.RootElement);
        _dsmModel.AssignElementOrder();
        return typeGraph.VertexCount;
    }

    /// <summary>
    ///     Finds all passthrough namespaces in the type graph. All namespaces that have only a single child, another
    ///     namespace.
    ///     These namespaces are merged for the view.
    ///     For example "A" -> "B" -> "C" -> "Type" gets to "A.B.C" -> "Type"
    ///     Counting the children on the code graph is enough, the type graph was built from the code graph.
    /// </summary>
    /// <remarks>
    ///     <list type="number">
    ///         <item>
    ///             An expand that reveals nothing. Expanding the element yields exactly one row, carrying the
    ///             same numbers as before. So it takes another click to see anything.
    ///         </item>
    ///         <item>
    ///             A vertical strip down the left of the matrix, taking width and saying nothing.
    ///         </item>
    ///         <item>
    ///             One of only four depth colors. MatrixColorConverter.GetColor computes
    ///             <c>depth % 4</c>, so an empty level shifts the whole ramp and brings it round sooner,
    ///             costing the depth-colored blocks the contrast they read by. That is the matrix's main
    ///             reading aid: inside the block is internal, outside crosses the boundary.
    ///         </item>
    ///     </list>
    /// </remarks>
    private HashSet<string> FindPassThroughNamespaces()
    {
        return _codeGraph.Nodes.Values
            .Where(element => element is { ElementType: CodeElementType.Namespace, IsExternal: false } &&
                              element.Children.Count == 1 &&
                              element.Children.First().ElementType is CodeElementType.Namespace)
            .Select(element => element.Id)
            .ToHashSet();
    }

    /// <summary>
    ///     Orders the children of every element so that dependencies line up on one side of the diagonal.
    /// </summary>
    /// <remarks>
    ///     The partitioning is what turns "no cycles" into the triangular shape that makes it visible. Sorting is
    ///     per parent: children are only ever reordered among their siblings, so the hierarchy is untouched.
    ///     Mirrors ImporterBase.Partition, which we cannot reuse (protected, and tied to the DSI import).
    /// </remarks>
    private void Partition(IDsmElement element)
    {
        var algorithm = SortAlgorithmFactory.CreateAlgorithm(_dsmModel, element, PartitionSortAlgorithm.AlgorithmName);
        _dsmModel.ReorderChildren(element, algorithm.Sort());

        foreach (var child in element.Children)
        {
            Partition(child);
        }
    }

    /// <summary>
    ///     Returns the DSM element for a code element, creating it and every missing ancestor first.
    ///     For a pass-through namespace it returns the nearest kept ancestor instead, which is what drops it from the model.
    /// </summary>
    private IDsmElement? AddWithAncestors(string codeElementId)
    {
        if (_passThroughNamespaces.Contains(codeElementId))
        {
            var skipped = _codeGraph.Nodes[codeElementId].Parent;
            return skipped is null ? null : AddWithAncestors(skipped.Id);
        }

        if (_dsmElementsByCodeElementId.TryGetValue(codeElementId, out var existing))
        {
            return existing;
        }

        var codeElement = _codeGraph.Nodes[codeElementId];
        var parent = codeElement.Parent is null ? null : AddWithAncestors(codeElement.Parent.Id);

        var dsmElement = _dsmModel.AddElement(
            MergedName(codeElement),
            codeElement.ElementType.ToString(),
            parent?.Id,
            null,
            null);

        _dsmElementsByCodeElementId[codeElementId] = dsmElement;
        return dsmElement;
    }

    /// <summary>
    ///     The element's name, prefixed with the pass-through ancestors that were dropped, so that
    ///     <c>A -> B -> C</c> with A and B pass-through becomes one element named <c>A.B.C</c>.
    ///     Types are never affected: a namespace holding a type has a non-namespace child and is
    ///     therefore not a pass-through, so the parent of a type is never dropped.
    /// </summary>
    private string MergedName(CodeElement element)
    {
        var parts = new List<string> { element.Name };

        var current = element.Parent;
        while (current is not null && _passThroughNamespaces.Contains(current.Id))
        {
            parts.Insert(0, current.Name);
            current = current.Parent;
        }

        return string.Join(".", parts);
    }

    private void AddRelations(TypeGraph typeGraph)
    {
        foreach (var (consumerId, providerIds) in typeGraph.Out)
        {
            var consumer = _dsmElementsByCodeElementId[consumerId];
            foreach (var providerId in providerIds)
            {
                // TypeGraph deduplicates, so every edge carries the same weight. Once it can report how
                // many relationships a type level edge stands for, that count belongs here: it is what
                // drives the matrix' cell weights.
                _dsmModel.AddRelation(consumer, _dsmElementsByCodeElementId[providerId], RelationType, 1, null);
            }
        }
    }
}