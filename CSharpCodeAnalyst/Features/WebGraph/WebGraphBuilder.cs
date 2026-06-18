using System.Text.Json;
using CodeGraph.Colors;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.WebGraph;

/// <summary>
///     Transforms a <see cref="CodeGraph.Graph.CodeGraph" /> into the JSON shape that
///     the Cytoscape front-end (app.js / renderGraph) expects: { nodes: [...], edges: [...] }.
///     This is the web pendant to <c>MsaglBuilderBase</c>.
///     Phase 1: renders the whole graph (expanded). Collapse/hide handling comes later.
/// </summary>
internal static class WebGraphBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <param name="isCollapsed">
    ///     Mirrors the Code Explorer: returns true for elements whose children are hidden.
    ///     Children of a collapsed element are not emitted, and edges into them are
    ///     rerouted to the collapsed container (same idea as MsaglHierarchicalBuilder).
    /// </param>
    public static string BuildJson(CodeGraph.Graph.CodeGraph graph, Func<string, bool> isCollapsed,
        bool showFlat, bool showInformationFlow)
    {
        var dto = showFlat
            ? BuildFlat(graph, showInformationFlow)
            : Build(graph, isCollapsed, showInformationFlow);

        // System.Text.Json's default encoder already escapes characters that would be
        // unsafe inside a <script> / JS string literal (e.g. U+2028, U+2029, <, >, &),
        // so the result can be embedded directly via ExecuteScriptAsync.
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    ///     Flat view (mirrors MsaglFlatBuilder): every element is a top-level node — no
    ///     compound nesting and no collapse. Each relationship is its own direct edge
    ///     (no rerouting, no bundling), and containment is drawn as explicit gray edges.
    /// </summary>
    private static WebGraphDto BuildFlat(CodeGraph.Graph.CodeGraph graph, bool showInformationFlow)
    {
        var dto = new WebGraphDto();

        foreach (var element in graph.Nodes.Values)
        {
            dto.Nodes.Add(new WebNodeDto
            {
                Id = element.Id,
                Label = element.Name,
                Kind = element.ElementType.ToString(),
                Parent = null,
                External = element.IsExternal,
                Color = ToHexColor(element),
                Collapsed = false
            });
        }

        var seenEdgeIds = new HashSet<string>();
        foreach (var relationship in graph.GetAllRelationships())
        {
            if (relationship.Type == RelationshipType.Containment)
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
            if (seenEdgeIds.Add(id))
            {
                dto.Edges.Add(new WebEdgeDto
                {
                    Id = id,
                    Source = source,
                    Target = target,
                    Kind = relationship.Type.ToString(),
                    Count = 1
                });
            }
        }

        // Containment as explicit edges, since there is no nesting in flat mode.
        foreach (var element in graph.Nodes.Values)
        {
            if (element.Parent is null)
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

        return dto;
    }

    /// <summary>
    ///     Returns the original relationships behind the (possibly bundled) edge between
    ///     two visible nodes, so a click on that edge can be explained in the Info panel.
    /// </summary>
    public static List<Relationship> GetBundledRelationships(CodeGraph.Graph.CodeGraph graph,
        Func<string, bool> isCollapsed, bool showFlat, bool showInformationFlow, string sourceId, string targetId)
    {
        var visible = showFlat ? null : ComputeVisibleSet(graph, isCollapsed);
        var result = new List<Relationship>();

        foreach (var relationship in graph.GetAllRelationships())
        {
            if (relationship.Type == RelationshipType.Containment)
            {
                continue;
            }

            // Flat: match direct endpoints; hierarchical: match rerouted endpoints.
            var (source, target) = visible is null
                ? FlatEdge(relationship, graph, showInformationFlow)
                : ResolveEdge(relationship, graph, visible, showInformationFlow);

            if (source == sourceId && target == targetId)
            {
                result.Add(relationship);
            }
        }

        return result;
    }

    private static (string Source, string Target) FlatEdge(Relationship relationship,
        CodeGraph.Graph.CodeGraph graph, bool showInformationFlow)
    {
        var source = relationship.SourceId;
        var target = relationship.TargetId;
        if (showInformationFlow && ShouldReverseInFlowMode(graph, source, relationship.Type))
        {
            (source, target) = (target, source);
        }

        return (source, target);
    }

    private static WebGraphDto Build(CodeGraph.Graph.CodeGraph graph, Func<string, bool> isCollapsed, bool showInformationFlow)
    {
        // 1. Visible node set: walk from the roots, stop descending at collapsed nodes.
        var visible = ComputeVisibleSet(graph, isCollapsed);

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
                // mark it as expandable (like the bold label in the MSAGL view).
                Collapsed = element.Children.Count > 0 && isCollapsed(element.Id)
            });
        }

        // 2. Edges: move each endpoint up to its nearest visible ancestor, then BUNDLE
        //    all relationships between the same (source, target) pair into a single edge.
        //    Without bundling, many parallel edges between few nodes pile up on the same
        //    path and pull the nodes on top of each other in the layout.
        var bundles = new Dictionary<(string Source, string Target), EdgeBundle>();
        foreach (var relationship in graph.GetAllRelationships())
        {
            // Containment is expressed through node nesting (parent), not as an edge.
            if (relationship.Type == RelationshipType.Containment)
            {
                continue;
            }

            var (sourceId, targetId) = ResolveEdge(relationship, graph, visible, showInformationFlow);
            if (sourceId is null || targetId is null)
            {
                continue;
            }

            var key = (sourceId, targetId);
            if (!bundles.TryGetValue(key, out var bundle))
            {
                bundle = new EdgeBundle();
                bundles[key] = bundle;
            }

            bundle.Add(relationship.Type);
        }

        foreach (var ((sourceId, targetId), bundle) in bundles)
        {
            dto.Edges.Add(new WebEdgeDto
            {
                Id = $"{sourceId}|{targetId}",
                Source = sourceId,
                Target = targetId,
                Kind = bundle.Kind(),
                Count = bundle.Count
            });
        }

        return dto;
    }

    private static HashSet<string> ComputeVisibleSet(CodeGraph.Graph.CodeGraph graph, Func<string, bool> isCollapsed)
    {
        // Walk from the roots, stop descending at collapsed nodes.
        var visible = new HashSet<string>();
        foreach (var root in graph.Nodes.Values.Where(n => n.Parent is null))
        {
            CollectVisible(root, isCollapsed, visible);
        }

        return visible;
    }

    private static void CollectVisible(CodeElement node, Func<string, bool> isCollapsed, HashSet<string> visible)
    {
        visible.Add(node.Id);

        // A collapsed node is visible itself, but its children are hidden.
        if (isCollapsed(node.Id))
        {
            return;
        }

        foreach (var child in node.Children)
        {
            CollectVisible(child, isCollapsed, visible);
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
    ///     edge should not be drawn (endpoint missing or a collapsed-internal self-loop).
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
        if (source is null || target is null || source == target)
        {
            return (null, null);
        }

        if (showInformationFlow && ShouldReverseInFlowMode(graph, source, relationship.Type))
        {
            (source, target) = (target, source);
        }

        return (source, target);
    }

    /// <summary>
    ///     Mirrors MsaglBuilderBase: in flow mode, structural edges point "downstream"
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
    }

    private sealed class WebEdgeDto
    {
        public required string Id { get; init; }
        public required string Source { get; init; }
        public required string Target { get; init; }
        public required string Kind { get; init; }
        public int Count { get; init; }
    }

    /// <summary>
    ///     Aggregates all relationships between one (source, target) pair into a single
    ///     visual edge. A single relationship keeps its own type; a real bundle (more than
    ///     one) becomes <see cref="RelationshipType.Bundled" /> — same convention as
    ///     MsaglHierarchicalBuilder, where Bundled is itself an edge type.
    /// </summary>
    private sealed class EdgeBundle
    {
        private RelationshipType _firstType;
        public int Count { get; private set; }

        public void Add(RelationshipType type)
        {
            if (Count == 0)
            {
                _firstType = type;
            }

            Count++;
        }

        public string Kind()
        {
            return Count == 1 ? _firstType.ToString() : RelationshipType.Bundled.ToString();
        }
    }
}
