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
    private readonly IDsmModel _dsmModel;

    /// <summary>Maps a code element id to the DSM element created for it.</summary>
    private readonly Dictionary<string, IDsmElement> _dsmElementsByCodeElementId = [];

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
        _passThroughNamespaces = FindPassThroughNamespaces(typeGraph);

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
    ///     The namespaces that carry no structure of their own: exactly one child, and that child another
    ///     namespace. They are left out, so their child hangs off the nearest ancestor that does say something.
    /// </summary>
    /// <remarks>
    ///     The parser creates one element per namespace segment, so a project whose root namespace repeats its
    ///     assembly name produces a chain of pass-throughs: assembly "A.B" holds namespace "A" holds namespace
    ///     "B" holds the real ones. Every one of those is a row and a column in the matrix, and they all read
    ///     the same. Dropping them is a view concern, which is why this lives here rather than in the parser,
    ///     whose hierarchy is right and is what the tree view wants.
    ///     <para>
    ///         "Exactly one child" has to be counted over what actually reaches the model, not over the code
    ///         graph: a namespace holding two types of which one is external is a pass-through here even
    ///         though the code graph shows it branching.
    ///     </para>
    /// </remarks>
    private HashSet<string> FindPassThroughNamespaces(TypeGraph typeGraph)
    {
        // Everything the model will hold: the types, plus every ancestor they hang from.
        var included = new HashSet<string>();
        foreach (var typeId in typeGraph.Vertices)
        {
            var current = _codeGraph.Nodes[typeId];
            while (current is not null && included.Add(current.Id))
            {
                current = current.Parent;
            }
        }

        var childrenByParent = new Dictionary<string, List<CodeElement>>();
        foreach (var element in included.Select(id => _codeGraph.Nodes[id]))
        {
            if (element.Parent is null)
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(element.Parent.Id, out var siblings))
            {
                siblings = [];
                childrenByParent[element.Parent.Id] = siblings;
            }

            siblings.Add(element);
        }

        var passThrough = new HashSet<string>();
        foreach (var element in included.Select(id => _codeGraph.Nodes[id]))
        {
            if (element.ElementType is not CodeElementType.Namespace)
            {
                continue;
            }

            if (childrenByParent.TryGetValue(element.Id, out var children) &&
                children is [{ ElementType: CodeElementType.Namespace }])
            {
                passThrough.Add(element.Id);
            }
        }

        return passThrough;
    }

    /// <summary>
    ///     Orders the children of every element so that dependencies line up on one side of the diagonal.
    /// </summary>
    /// <remarks>
    ///     Without this the rows sit in whatever order the elements were added, which for us is the iteration
    ///     order of a hash set — so an acyclic structure looks just as scattered as a tangled one. The
    ///     partitioning is what turns "no cycles" into the triangular shape that makes it visible. Sorting is
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
    ///     Returns the DSM element for a code element, creating it and every missing ancestor first. Walking up
    ///     before creating keeps a parent's id available by the time the child needs it. For a pass-through
    ///     namespace it returns the nearest kept ancestor instead, which is what drops it from the model.
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
            codeElement.Name,
            codeElement.ElementType.ToString(),
            parent?.Id,
            null,
            null);

        _dsmElementsByCodeElementId[codeElementId] = dsmElement;
        return dsmElement;
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
