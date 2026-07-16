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
    ///     before creating keeps a parent's id available by the time the child needs it.
    /// </summary>
    private IDsmElement AddWithAncestors(string codeElementId)
    {
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
