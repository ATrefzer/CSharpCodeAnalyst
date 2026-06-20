using System.Text.Json;
using CodeGraph.Colors;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Features.Graph;
using CSharpCodeAnalyst.Features.Graph.Filtering;

namespace CSharpCodeAnalyst.Features.WebGraph;

/// <summary>
///     Transforms a <see cref="CodeGraph.Graph.CodeGraph" /> into the JSON shape that
///     the Cytoscape front-end (app.js / renderGraph) expects: { nodes: [...], edges: [...] }.
///     It honours the presentation state (collapse/expand) and the <see cref="GraphHideFilter" /> (hidden element and
///     relationship types).
///     Besides the JSON it returns the <see cref="WebEdgeInfo" />. This is a lookup table
///     edge id -> relationship (a bundled edge has a list of relationships). So we don't attach it as edge data.
/// </summary>
internal static class WebGraphBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static WebGraphData Build(CodeGraph.Graph.CodeGraph graph, Func<string, bool> isCollapsed,
        bool showFlat, bool showInformationFlow, GraphHideFilter hideFilter, PresentationState presentationState)
    {
        var (dto, edges) = showFlat
            ? BuildFlat(graph, showInformationFlow, hideFilter, presentationState)
            : BuildHierarchical(graph, isCollapsed, showInformationFlow, hideFilter, presentationState);

        // System.Text.Json's default encoder already escapes characters that would be
        // unsafe inside a <script> / JS string literal (e.g. U+2028, U+2029, <, >, &),
        // so the result can be embedded directly via ExecuteScriptAsync.
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        return new WebGraphData(json, edges, dto.Nodes.Count);
    }

    /// <summary>
    ///     Flat view: every element is a top-level node — no
    ///     compound nesting and no collapse. Each relationship type between a pair is its own
    ///     direct edge (no rerouting, no cross-type bundling), and containment is drawn as
    ///     explicit gray edges.
    /// </summary>
    private static (WebGraphDto Dto, Dictionary<string, WebEdgeInfo> Edges) BuildFlat(
        CodeGraph.Graph.CodeGraph graph, bool showInformationFlow, GraphHideFilter hideFilter,
        PresentationState presentationState)
    {
        var dto = new WebGraphDto();
        var edges = new Dictionary<string, WebEdgeInfo>();

        foreach (var element in graph.Nodes.Values)
        {
            // The hide filter removes whole element types from the view.
            if (hideFilter.ShouldHideElement(element))
            {
                continue;
            }

            dto.Nodes.Add(new WebNodeDto
            {
                Id = element.Id,
                Label = element.Name,
                Kind = element.ElementType.ToString(),
                Parent = null,
                External = element.IsExternal,
                Color = ToHexColor(element),
                Collapsed = false,
                Flagged = presentationState.IsFlagged(element.Id),
                SearchHighlighted = presentationState.IsSearchHighlighted(element.Id)
            });
        }

        // One drawn edge per (source, type, target). Relationships of the same type between
        // the same pair (e.g. two distinct Calls) collapse onto that edge and are kept as
        // its metadata, so a click reports all of them.
        var accumulators = new Dictionary<string, EdgeAccumulator>();
        foreach (var relationship in graph.GetAllRelationships())
        {
            if (relationship.Type == RelationshipType.Containment)
            {
                continue;
            }

            // Skip hidden relationship types and edges touching a hidden element.
            if (hideFilter.ShouldHideRelationship(relationship) ||
                hideFilter.ShouldHideElement(graph.Nodes[relationship.SourceId]) ||
                hideFilter.ShouldHideElement(graph.Nodes[relationship.TargetId]))
            {
                continue;
            }

            var source = relationship.SourceId;
            var target = relationship.TargetId;
            if (showInformationFlow && ShouldReverseInFlowMode(graph, source, relationship.Type))
            {
                (source, target) = (target, source);
            }

            var id = $"{source}|{relationship.Type}|{target}";
            if (!accumulators.TryGetValue(id, out var accumulator))
            {
                accumulator = new EdgeAccumulator(source, target);
                accumulators[id] = accumulator;
            }

            accumulator.Add(relationship);
        }

        foreach (var (id, accumulator) in accumulators)
        {
            // In flat mode an edge is always a single relationship type (the id carries it),
            // so color and kind come straight from that type — never "Bundled".
            var type = accumulator.Relationships[0].Type;
            dto.Edges.Add(new WebEdgeDto
            {
                Id = id,
                Source = accumulator.SourceId,
                Target = accumulator.TargetId,
                Kind = type.ToString(),
                Count = accumulator.Relationships.Count,
                Color = EdgeColor(type),
                Flagged = presentationState.IsFlagged((accumulator.SourceId, accumulator.TargetId))
            });
            edges[id] = accumulator.ToEdgeInfo();
        }

        // Containment as explicit edges, since there is no nesting in flat mode.
        foreach (var element in graph.Nodes.Values)
        {
            if (element.Parent is null || hideFilter.HiddenRelationshipTypes.Contains(RelationshipType.Containment))
            {
                continue;
            }

            // Don't draw containment into or out of a hidden element.
            if (hideFilter.ShouldHideElement(element) || hideFilter.ShouldHideElement(element.Parent))
            {
                continue;
            }

            dto.Edges.Add(new WebEdgeDto
            {
                Id = $"containment|{element.Parent.Id}|{element.Id}",
                Source = element.Parent.Id,
                Target = element.Id,
                Kind = "Containment",
                Count = 1
            });
        }

        return (dto, edges);
    }

    private static (WebGraphDto Dto, Dictionary<string, WebEdgeInfo> Edges) BuildHierarchical(
        CodeGraph.Graph.CodeGraph graph, Func<string, bool> isCollapsed, bool showInformationFlow, GraphHideFilter hideFilter,
        PresentationState presentationState)
    {
        // 1. Visible node set: walk from the roots, stop descending at collapsed nodes
        //    and prune hidden subtrees.
        var visible = ComputeVisibleSet(graph, isCollapsed, hideFilter);

        var dto = new WebGraphDto();
        foreach (var id in visible)
        {
            var element = graph.Nodes[id];

            // The direct parent of a visible node is always visible (we only descend
            // into expanded nodes), so it is safe to reference it as the compound parent.
            var parentId = element.Parent != null && visible.Contains(element.Parent.Id)
                ? element.Parent.Id
                : null;

            dto.Nodes.Add(new WebNodeDto
            {
                Id = element.Id,
                Label = element.Name,
                Kind = element.ElementType.ToString(),
                Parent = parentId,
                External = element.IsExternal,
                Color = ToHexColor(element),
                // A collapsed container is drawn as a leaf here; flag it so the UI can
                // mark it as expandable.
                Collapsed = element.Children.Count > 0 && isCollapsed(element.Id),
                Flagged = presentationState.IsFlagged(element.Id),
                SearchHighlighted = presentationState.IsSearchHighlighted(element.Id)
            });
        }

        // 2. Edges: move each endpoint up to its nearest visible ancestor, then BUNDLE
        //    all relationships between the same (source, target) pair into a single edge.
        //    Without bundling, many parallel edges between few nodes pile up on the same
        //    path and pull the nodes on top of each other in the layout.
        var accumulators = new Dictionary<(string Source, string Target), EdgeAccumulator>();
        foreach (var relationship in graph.GetAllRelationships())
        {
            // Containment is expressed through node nesting (parent), not as an edge.
            if (relationship.Type == RelationshipType.Containment)
            {
                continue;
            }

            // Hidden relationship types never appear. Hidden endpoints are already gone
            // from the visible set, so ResolveEdge reroutes them to a visible ancestor.
            if (hideFilter.ShouldHideRelationship(relationship))
            {
                continue;
            }

            var (sourceId, targetId) = ResolveEdge(relationship, graph, visible, showInformationFlow);
            if (sourceId is null || targetId is null)
            {
                continue;
            }

            var key = (sourceId, targetId);
            if (!accumulators.TryGetValue(key, out var accumulator))
            {
                accumulator = new EdgeAccumulator(sourceId, targetId);
                accumulators[key] = accumulator;
            }

            accumulator.Add(relationship);
        }

        var edges = new Dictionary<string, WebEdgeInfo>();
        foreach (var ((sourceId, targetId), accumulator) in accumulators)
        {
            var id = $"{sourceId}|{targetId}";
            dto.Edges.Add(new WebEdgeDto
            {
                Id = id,
                Source = sourceId,
                Target = targetId,
                Kind = accumulator.Kind(),
                Count = accumulator.Relationships.Count,
                Color = EdgeColor(accumulator.EffectiveType),
                Flagged = presentationState.IsFlagged((sourceId, targetId))
            });
            edges[id] = accumulator.ToEdgeInfo();
        }

        return (dto, edges);
    }

    private static HashSet<string> ComputeVisibleSet(CodeGraph.Graph.CodeGraph graph, Func<string, bool> isCollapsed,
        GraphHideFilter hideFilter)
    {
        // Walk from the roots, stop descending at collapsed nodes.
        var visible = new HashSet<string>();
        foreach (var root in graph.Nodes.Values.Where(n => n.Parent is null))
        {
            CollectVisible(root, isCollapsed, hideFilter, visible);
        }

        return visible;
    }

    private static void CollectVisible(CodeElement node, Func<string, bool> isCollapsed, GraphHideFilter hideFilter,
        HashSet<string> visible)
    {
        if (hideFilter.ShouldHideElement(node))
        {
            return;
        }

        visible.Add(node.Id);

        // A collapsed node is visible itself, but its children are hidden.
        if (isCollapsed(node.Id))
        {
            return;
        }

        foreach (var child in node.Children)
        {
            CollectVisible(child, isCollapsed, hideFilter, visible);
        }
    }

    private static string? NearestVisibleOrSelf(string id, CodeGraph.Graph.CodeGraph graph, HashSet<string> visible)
    {
        var current = graph.Nodes[id];
        while (current != null && !visible.Contains(current.Id))
        {
            current = current.Parent;
        }

        return current?.Id;
    }

    /// <summary>
    ///     Resolves a relationship to the (source, target) of the edge actually drawn:
    ///     endpoints rerouted to their nearest visible ancestor, then reversed for certain
    ///     structural kinds when information-flow mode is on. Returns (null, null) when the
    ///     edge should not be drawn (endpoint missing, or a self-loop that is merely an
    ///     artifact of collapsing — a *genuine* self-reference such as recursion is kept).
    /// </summary>
    private static (string? Source, string? Target) ResolveEdge(Relationship relationship,
        CodeGraph.Graph.CodeGraph graph, HashSet<string> visible, bool showInformationFlow)
    {
        if (!graph.Nodes.ContainsKey(relationship.SourceId) ||
            !graph.Nodes.ContainsKey(relationship.TargetId))
        {
            return (null, null);
        }

        var source = NearestVisibleOrSelf(relationship.SourceId, graph, visible);
        var target = NearestVisibleOrSelf(relationship.TargetId, graph, visible);
        if (source is null || target is null)
        {
            return (null, null);
        }

        if (showInformationFlow && ShouldReverseInFlowMode(graph, source, relationship.Type))
        {
            (source, target) = (target, source);
        }

        // Drop a self-loop only when BOTH endpoints were rerouted up to the same container
        // (an internal edge of a collapsed node). A real self-reference — where an original
        // endpoint already IS that node — is drawn.
        if (source == target &&
            relationship.SourceId != source && relationship.TargetId != target)
        {
            return (null, null);
        }

        return (source, target);
    }

    /// <summary>
    ///     In flow mode, structural edges point "downstream"
    ///     (interface -> implementation, base -> override, event -> handler).
    /// </summary>
    private static bool ShouldReverseInFlowMode(CodeGraph.Graph.CodeGraph graph, string sourceId, RelationshipType type)
    {
        // An event implementing an interface member keeps its natural direction.
        if (type == RelationshipType.Implements && graph.Nodes[sourceId].ElementType == CodeElementType.Event)
        {
            return false;
        }

        return type is RelationshipType.Handles or RelationshipType.Implements or RelationshipType.Overrides;
    }

    private static string ToHexColor(CodeElement element)
    {
        if (element.IsExternal)
        {
            return "#808080";
        }

        var rgb = ColorDefinitions.GetRbgOf(element.ElementType);
        return $"#{rgb:X6}";
    }

    /// <summary>
    ///     Optional per-edge color by relationship type. Returns null for "default" (black),
    ///     so app.js only overrides the line color where we actually want a tint.
    ///     Experimental: Call edges are tinted blue (related to the blue Method nodes).
    /// </summary>
    private static string? EdgeColor(RelationshipType type)
    {
        return type == RelationshipType.Calls ? "#1976D2" : null;
    }

    private sealed class WebGraphDto
    {
        public List<WebNodeDto> Nodes { get; } = [];
        public List<WebEdgeDto> Edges { get; } = [];
    }

    private sealed class WebNodeDto
    {
        public required string Id { get; init; }
        public required string Label { get; init; }
        public required string Kind { get; init; }
        public string? Parent { get; init; }
        public bool External { get; init; }
        public required string Color { get; init; }
        public bool Collapsed { get; init; }

        // Presentation decorations (rendered as red borders).
        public bool Flagged { get; init; }
        public bool SearchHighlighted { get; init; }
    }

    private sealed class WebEdgeDto
    {
        public required string Id { get; init; }
        public required string Source { get; init; }
        public required string Target { get; init; }
        public required string Kind { get; init; }
        public int Count { get; init; }

        // Optional per-edge line color (hex). Null means "use the default edge color".
        public string? Color { get; init; }

        // Edge flag decoration (the (source, target) pair was flagged by the user).
        public bool Flagged { get; init; }
    }

    /// <summary>
    ///     Collects the relationships a single drawn edge stands for, together with the
    ///     drawn (rerouted) endpoints. A single relationship keeps its own type; a real
    ///     bundle (more than one) becomes <see cref="RelationshipType.Bundled" />
    /// </summary>
    private sealed class EdgeAccumulator(string sourceId, string targetId)
    {
        public string SourceId { get; } = sourceId;
        public string TargetId { get; } = targetId;
        public List<Relationship> Relationships { get; } = [];

        // A single relationship keeps its own type; a real bundle is RelationshipType.Bundled.
        public RelationshipType EffectiveType => Relationships.Count == 1 ? Relationships[0].Type : RelationshipType.Bundled;

        public void Add(Relationship relationship)
        {
            Relationships.Add(relationship);
        }

        public string Kind()
        {
            return EffectiveType.ToString();
        }

        public WebEdgeInfo ToEdgeInfo()
        {
            return new WebEdgeInfo(SourceId, TargetId, Relationships);
        }
    }
}

/// <summary>
///     The result of <see cref="WebGraphBuilder.Build" />: the Cytoscape JSON plus the
///     relationships behind every drawn edge, keyed by edge id. <see cref="NodeCount" /> is
///     the number of nodes that will be drawn (used for the large-graph warning).
/// </summary>
internal sealed record WebGraphData(string Json, IReadOnlyDictionary<string, WebEdgeInfo> Edges, int NodeCount);

/// <summary>
///     Metadata (relationships)for each drawn edge.
///     Bundled edges have multiple relationships of different types; unbundled edges have exactly one relationship.
/// </summary>
internal sealed record WebEdgeInfo(string SourceId, string TargetId, List<Relationship> Relationships);