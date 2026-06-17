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
    public static string BuildJson(CodeGraph.Graph.CodeGraph graph, Func<string, bool> isCollapsed)
    {
        var dto = Build(graph, isCollapsed);

        // System.Text.Json's default encoder already escapes characters that would be
        // unsafe inside a <script> / JS string literal (e.g. U+2028, U+2029, <, >, &),
        // so the result can be embedded directly via ExecuteScriptAsync.
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    ///     Returns the original relationships behind the (possibly bundled) edge between
    ///     two visible nodes, so a click on that edge can be explained in the Info panel.
    /// </summary>
    public static List<Relationship> GetBundledRelationships(CodeGraph.Graph.CodeGraph graph,
        Func<string, bool> isCollapsed, string sourceId, string targetId)
    {
        var visible = ComputeVisibleSet(graph, isCollapsed);
        var result = new List<Relationship>();

        foreach (var relationship in graph.GetAllRelationships())
        {
            if (relationship.Type == RelationshipType.Containment)
            {
                continue;
            }

            if (!graph.Nodes.ContainsKey(relationship.SourceId) ||
                !graph.Nodes.ContainsKey(relationship.TargetId))
            {
                continue;
            }

            var source = NearestVisibleOrSelf(relationship.SourceId, graph, visible);
            var target = NearestVisibleOrSelf(relationship.TargetId, graph, visible);
            if (source == sourceId && target == targetId)
            {
                result.Add(relationship);
            }
        }

        return result;
    }

    private static WebGraphDto Build(CodeGraph.Graph.CodeGraph graph, Func<string, bool> isCollapsed)
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
                Color = ToHexColor(element)
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

            if (!graph.Nodes.ContainsKey(relationship.SourceId) ||
                !graph.Nodes.ContainsKey(relationship.TargetId))
            {
                continue;
            }

            var sourceId = NearestVisibleOrSelf(relationship.SourceId, graph, visible);
            var targetId = NearestVisibleOrSelf(relationship.TargetId, graph, visible);
            if (sourceId is null || targetId is null || sourceId == targetId)
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
                Kind = bundle.DominantKind(),
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
    ///     visual edge and decides which kind should drive its styling.
    /// </summary>
    private sealed class EdgeBundle
    {
        private readonly HashSet<RelationshipType> _types = [];
        public int Count { get; private set; }

        public void Add(RelationshipType type)
        {
            _types.Add(type);
            Count++;
        }

        /// <summary>
        ///     Call-like edges win (keep "blue arrow = a call" readable), then the
        ///     structural kinds, otherwise a generic "Uses".
        /// </summary>
        public string DominantKind()
        {
            if (_types.Contains(RelationshipType.Calls) || _types.Contains(RelationshipType.Invokes))
            {
                return "Calls";
            }

            if (_types.Contains(RelationshipType.Inherits))
            {
                return "Inherits";
            }

            if (_types.Contains(RelationshipType.Implements))
            {
                return "Implements";
            }

            return "Uses";
        }
    }
}
