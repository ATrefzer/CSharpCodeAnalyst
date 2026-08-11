using System.ComponentModel;
using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Exploration;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     Tools that follow relationships: what an element depends on, what depends on it, and how two
///     elements are connected.
/// </summary>
[McpServerToolType]
public sealed class RelationshipTools(ICodeGraphSnapshotSource snapshotSource)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 300;

    /// <summary>
    ///     Enough to see the shape of a bundle without turning one question into a wall of text.
    /// </summary>
    private const int MaxRenderedPaths = 15;

    [McpServerTool(Name = "find_outgoing_relationships", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "What an element depends on: the relationships that start at it - calls, uses, inherits, " +
        "implements and so on.\n" +
        "deep=true additionally follows relationships that start at contained elements, so asking a " +
        "class covers what its methods depend on. It reports only what LEAVES the element: a call " +
        "from one method of the class to another stays inside and is not listed. To see those, ask " +
        "about the member itself with deep=false.")]
    public async Task<string> FindOutgoingRelationshipsAsync(
        [Description("Element id from search_elements.")]
        string id,
        [Description("Include relationships that start at contained elements and point outside the " +
                     "element. Default false.")]
        bool deep = false,
        [Description("Maximum number of relationships listed (default 50, capped at 300).")]
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        return await ExploreAsync(id, limit, true, deep, cancellationToken);
    }

    [McpServerTool(Name = "find_incoming_relationships", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "What depends on an element: the relationships that end at it. This is the blast radius of a " +
        "change.\n" +
        "deep=true additionally follows relationships that end at contained elements, so asking a " +
        "class covers everything reaching into its members. It reports only what comes from OUTSIDE " +
        "the element: one member using another stays inside and is not listed.")]
    public async Task<string> FindIncomingRelationshipsAsync(
        [Description("Element id from search_elements.")]
        string id,
        [Description("Include relationships that end at contained elements and come from outside the " +
                     "element. Default false.")]
        bool deep = false,
        [Description("Maximum number of relationships listed (default 50, capped at 300).")]
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        return await ExploreAsync(id, limit, false, deep, cancellationToken);
    }

    [McpServerTool(Name = "find_incoming_calls", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Who calls a method, transitively - the full chain of callers, not just the direct ones.\n" +
        "followAbstractions=true (the default) also treats a call to an interface or base declaration " +
        "as reaching the implementation, which is what you want for 'who can end up here'. It is a " +
        "heuristic: the graph is static and cannot know which implementation runs, so a reported " +
        "caller may never actually reach this method at runtime.\n" +
        "followAbstractions=false follows only direct call edges. Every result is then certain, but " +
        "callers that arrive through virtual dispatch or events are missing - an empty result does " +
        "NOT mean nothing calls it.")]
    public async Task<string> FindIncomingCallsAsync(
        [Description("Id of a method, from search_elements.")]
        string id,
        [Description("Follow interface and base declarations. Default true.")]
        bool followAbstractions = true,
        [Description("Maximum number of callers listed (default 50, capped at 300).")]
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var element = snapshot.Graph.TryGetCodeElement(id);
        if (element is null)
        {
            return ToolText.UnknownId(id);
        }

        var explorer = CreateExplorer(snapshot);
        var result = followAbstractions
            ? explorer.FollowIncomingCallsHeuristically(id)
            : explorer.FindIncomingCallsRecursive(id);

        // The start method is part of the traversal and comes back with the result; as a "caller of
        // itself" it is noise.
        var callers = result.Elements.Where(caller => caller.Id != id).ToList();

        var text = new StringBuilder();
        text.Append("Callers of ").AppendLine(ElementFormatter.Line(element, snapshot.SourceRoot));

        if (followAbstractions)
        {
            text.AppendLine(
                "Heuristic. The list contains callers that only reach this method through an interface " +
                "or base declaration - some of them may never reach it at runtime - and the " +
                "declarations themselves, which are steps on the route rather than callers.");
        }
        else
        {
            text.AppendLine(
                "Direct call edges only. Callers going through virtual dispatch or events are not " +
                "listed - an empty result does not prove the method is unused.");
        }

        text.AppendLine();

        if (callers.Count == 0)
        {
            text.AppendLine("No callers found.");
            return text.ToString();
        }

        text.Append(callers.Count.ToString(CultureInfo.InvariantCulture)).Append(" caller(s): ")
            .AppendLine(ElementFormatter.Summarize(callers, c => c.ElementType.ToString()));

        var effectiveLimit = Math.Clamp(limit, 1, MaxLimit);

        // A caller set cannot be narrowed - the question is already as specific as it gets - so the
        // only way to the rest is a higher limit, and once that is spent there is none.
        var hint = effectiveLimit < MaxLimit
            ? $"Raise limit (up to {MaxLimit.ToString(CultureInfo.InvariantCulture)}) to see the rest."
            : "The limit is already at its maximum; ask about a caller further down to go on from there.";

        ElementFormatter.AppendLimited(text, callers, snapshot.SourceRoot, effectiveLimit, hint);
        return text.ToString();
    }

    [McpServerTool(Name = "find_inheritance", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "The inheritance hierarchy around an element, both ways and over any number of levels: what it " +
        "derives from or implements, and what derives from or implements it. This is the tool for 'who " +
        "implements this interface' and 'what does this class actually extend'.\n" +
        "Works for a type and for a member alike - asking a method reports the declarations it " +
        "overrides and the overrides beneath it. Only inheritance counts: a class that merely uses the " +
        "element is not here, use find_incoming_relationships for that.\n" +
        "The answer is limited to the analyzed code. An implementation living in code the parser never " +
        "saw is missing, so an empty result downwards means 'none in this code base', not 'none'.")]
    public async Task<string> FindInheritanceAsync(
        [Description("Element id from search_elements.")]
        string id,
        [Description("Maximum number of entries listed per direction (default 50, capped at 300).")]
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var element = snapshot.Graph.TryGetCodeElement(id);
        if (element is null)
        {
            return ToolText.UnknownId(id);
        }

        // One scan, then two lookups. Walking the hierarchy by querying the graph per element would
        // rescan it once per step, and a widely implemented interface has a lot of steps.
        var edges = snapshot.Graph.GetAllRelationships().Where(IsInheritance).ToList();
        var upwards = WalkHierarchy(edges.ToLookup(edge => edge.SourceId), element.Id,
            edge => edge.TargetId);
        var downwards = WalkHierarchy(edges.ToLookup(edge => edge.TargetId), element.Id,
            edge => edge.SourceId);

        var text = new StringBuilder();
        text.Append("Inheritance around ")
            .AppendLine(ElementFormatter.Line(element, snapshot.SourceRoot));

        if (upwards.Count == 0 && downwards.Count == 0)
        {
            text.AppendLine();
            text.AppendLine(
                "No inheritance relationships. Nothing derives from it and it derives from nothing - " +
                "which says nothing about whether it is used; ask find_incoming_relationships for that.");
            return text.ToString();
        }

        // Only worth explaining when something is actually indented; most hierarchies are one level
        // deep, and there the sentence describes a layout the answer does not use.
        if (upwards.Concat(downwards).Any(entry => entry.Depth > 1))
        {
            text.AppendLine("Indentation is the distance in levels, one step per line of descent.");
        }

        text.AppendLine();

        var effectiveLimit = Math.Clamp(limit, 1, MaxLimit);

        AppendHierarchy(text, snapshot.Graph, "Derives from / implements", upwards,
            edge => edge.TargetId, snapshot.SourceRoot, effectiveLimit);
        AppendHierarchy(text, snapshot.Graph, "Derived from / implemented by", downwards,
            edge => edge.SourceId, snapshot.SourceRoot, effectiveLimit);

        return text.ToString();
    }

    [McpServerTool(Name = "find_paths_between", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "How two elements are connected: the shortest dependency chains from one to the other, " +
        "through any number of elements in between. Answers 'these two are related somehow, but how?'\n" +
        "Both elements are expanded to their contents first, so asking about two classes finds the " +
        "concrete chain between their methods. Only real dependencies are followed - containment is " +
        "not a path, or everything would be connected through a common ancestor. All chains of the " +
        "shortest length are reported, because one alone would hide whether the connection is a " +
        "single thin wire or a bundle.")]
    public async Task<string> FindPathsBetweenAsync(
        [Description("Id of the element the chain starts at.")]
        string sourceId,
        [Description("Id of the element the chain ends at.")]
        string targetId,
        [Description("Maximum number of relationships in a chain. Default 5. Pairs whose shortest " +
                     "chain is longer are reported as unconnected, which keeps an unrelated pair " +
                     "from pulling in half the graph.")]
        int maxLength = 5,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var source = snapshot.Graph.TryGetCodeElement(sourceId);
        if (source is null)
        {
            return ToolText.UnknownId(sourceId);
        }

        var target = snapshot.Graph.TryGetCodeElement(targetId);
        if (target is null)
        {
            return ToolText.UnknownId(targetId);
        }

        var explorer = CreateExplorer(snapshot);
        var result = explorer.FindPathsBetween([sourceId, targetId], Math.Max(1, maxLength));

        var text = new StringBuilder();
        text.Append("From ").AppendLine(ElementFormatter.Line(source, snapshot.SourceRoot));
        text.Append("To   ").AppendLine(ElementFormatter.Line(target, snapshot.SourceRoot));
        text.AppendLine();

        if (result.Relationships.Count == 0)
        {
            text.Append("No dependency chain of ")
                .Append(maxLength.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" relationships or fewer connects them in either direction.");
            text.AppendLine(
                "They may still be connected over a longer chain - raise maxLength - or only through " +
                "a shared parent, which is not a dependency and deliberately not reported as a path.");
            return text.ToString();
        }

        AppendPaths(text, snapshot.Graph, result, source, target, maxLength);
        AppendPaths(text, snapshot.Graph, result, target, source, maxLength);

        return text.ToString();
    }

    /// <summary>
    ///     The three relationship types that make up a hierarchy. Inherits and Implements carry it
    ///     between types, Overrides between members - and a member is a perfectly reasonable thing to
    ///     ask about, so all three belong to the same question.
    /// </summary>
    private static bool IsInheritance(Relationship relationship)
    {
        return relationship.Type is RelationshipType.Inherits or RelationshipType.Implements
            or RelationshipType.Overrides;
    }

    /// <summary>
    ///     Breadth-first away from the start, so an entry carries the number of levels it sits away.
    ///     Direction is decided entirely by the two arguments: pass the edges keyed by source and read
    ///     the target to walk up, key by target and read the source to walk down.
    ///     <para>
    ///         An element already seen is not visited again. That bounds a malformed graph that has a
    ///         cycle where the language allows none, and it collapses a diamond - a type reaching the
    ///         same ancestor through two interfaces is listed once, under the first route found.
    ///     </para>
    /// </summary>
    private static List<(int Depth, Relationship Edge)> WalkHierarchy(
        ILookup<string, Relationship> edgesFrom, string startId, Func<Relationship, string> stepTo)
    {
        var found = new List<(int Depth, Relationship Edge)>();
        var visited = new HashSet<string> { startId };
        var frontier = new List<string> { startId };
        var depth = 0;

        while (frontier.Count > 0)
        {
            depth++;
            var next = new List<string>();

            foreach (var currentId in frontier)
            {
                foreach (var edge in edgesFrom[currentId])
                {
                    var otherId = stepTo(edge);
                    if (!visited.Add(otherId))
                    {
                        continue;
                    }

                    found.Add((depth, edge));
                    next.Add(otherId);
                }
            }

            frontier = next;
        }

        return found;
    }

    /// <summary>
    ///     One direction of the hierarchy. The section is written even when it is empty, because
    ///     "nothing implements this" is an answer a caller came for just as much as a list.
    /// </summary>
    private static void AppendHierarchy(StringBuilder text, CodeGraph.Graph.CodeGraph graph,
        string title, List<(int Depth, Relationship Edge)> found, Func<Relationship, string> farId,
        string? sourceRoot, int limit)
    {
        text.Append(title).Append(" (").Append(found.Count.ToString(CultureInfo.InvariantCulture))
            .Append(')');

        if (found.Count == 0)
        {
            text.AppendLine(": none");
            text.AppendLine();
            return;
        }

        text.Append(": ")
            .AppendLine(ElementFormatter.Summarize(found, entry => entry.Edge.Type.ToString()));

        foreach (var (depth, edge) in found.Take(limit))
        {
            var far = graph.TryGetCodeElement(farId(edge));
            if (far is null)
            {
                continue;
            }

            text.Append("  ").Append(' ', (depth - 1) * 2);
            text.Append(edge.Type.ToString().PadRight(12)).Append(' ');
            text.AppendLine(ElementFormatter.Line(far, sourceRoot));
        }

        if (found.Count > limit)
        {
            var omitted = found.Count - limit;
            text.Append("  ... ").Append(omitted.ToString(CultureInfo.InvariantCulture))
                .Append(" more not shown (")
                .Append(found.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" in total). ")
                .AppendLine(limit < MaxLimit
                    ? $"Raise limit (up to {MaxLimit.ToString(CultureInfo.InvariantCulture)}) to see the rest."
                    : "The limit is already at its maximum; ask about one of the listed elements to go on.");
        }

        text.AppendLine();
    }

    /// <summary>
    ///     A fresh explorer per call, bound to the snapshot this call answers from. It holds the graph
    ///     in a field, so a shared instance would be a race the moment two calls straddle a snapshot
    ///     change - and creating one costs a single field assignment.
    /// </summary>
    private static CodeGraphExplorer CreateExplorer(GraphSnapshot snapshot)
    {
        var explorer = new CodeGraphExplorer();
        explorer.LoadCodeGraph(snapshot.Graph);
        return explorer;
    }

    private async Task<string> ExploreAsync(string id, int limit, bool outgoing, bool deep,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var element = snapshot.Graph.TryGetCodeElement(id);
        if (element is null)
        {
            return ToolText.UnknownId(id);
        }

        var explorer = CreateExplorer(snapshot);
        var result = outgoing
            ? deep ? explorer.FindOutgoingRelationshipsDeep(id) : explorer.FindOutgoingRelationships(id)
            : deep ? explorer.FindIncomingRelationshipsDeep(id) : explorer.FindIncomingRelationships(id);

        var text = new StringBuilder();
        text.Append(outgoing ? "Outgoing from " : "Incoming to ")
            .AppendLine(ElementFormatter.Line(element, snapshot.SourceRoot));

        if (deep)
        {
            // Without this the reader draws the wrong conclusion from a short result: the internal
            // relationships are missing by design, not because they do not exist.
            text.AppendLine(outgoing
                ? "Including contained elements. Only relationships leaving this element are listed - " +
                  "one member calling another is internal and not shown."
                : "Including contained elements. Only relationships arriving from outside this element " +
                  "are listed - one member using another is internal and not shown.");
        }

        if (element.IsExternal && outgoing)
        {
            text.AppendLine(
                "This element is external. Its own dependencies were never analyzed, so an empty " +
                "result says nothing about it.");
        }

        text.AppendLine();

        var relationships = result.Relationships;
        if (relationships.Count == 0)
        {
            text.AppendLine("No relationships found.");
            return text.ToString();
        }

        text.Append(relationships.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" relationship(s): ")
            .AppendLine(ElementFormatter.Summarize(relationships, r => r.Type.ToString()));
        text.AppendLine();

        RelationshipFormatter.Append(text, snapshot.Graph, relationships, element, outgoing,
            snapshot.SourceRoot, Math.Clamp(limit, 1, MaxLimit));

        return text.ToString();
    }

    /// <summary>
    ///     Turns the returned sub graph back into readable chains. The explorer answers with a set of
    ///     elements and relationships, not with ordered paths, so they are walked out again here - a
    ///     chain the reader can follow is worth far more than an edge list they have to reassemble.
    ///     <para>
    ///         Both ends are expanded to their contents by the search, so a chain usually starts at a
    ///         member rather than at the element that was asked about. The walk therefore begins at every
    ///         descendant of <paramref name="from" /> that the result contains.
    ///     </para>
    /// </summary>
    private static void AppendPaths(StringBuilder text, CodeGraph.Graph.CodeGraph graph,
        SearchResult result, CodeElement from, CodeElement to, int maxLength)
    {
        var edgesBySource = result.Relationships.ToLookup(relationship => relationship.SourceId);
        var starts = from.GetChildrenIncludingSelf();
        var ends = to.GetChildrenIncludingSelf();

        var paths = new List<List<Relationship>>();
        foreach (var startId in starts)
        {
            Walk(startId, [], new HashSet<string> { startId });
            if (paths.Count >= MaxRenderedPaths)
            {
                break;
            }
        }

        if (paths.Count == 0)
        {
            return;
        }

        text.Append(from.Name).Append(" -> ").Append(to.Name).Append("  (")
            .Append(paths.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(paths.Count >= MaxRenderedPaths ? "+ chains)" : " chain(s))");

        foreach (var path in paths)
        {
            text.Append("  ").AppendLine(RenderPath(graph, path));
        }

        text.AppendLine();
        return;

        void Walk(string currentId, List<Relationship> soFar, HashSet<string> visited)
        {
            if (paths.Count >= MaxRenderedPaths || soFar.Count >= maxLength)
            {
                return;
            }

            foreach (var edge in edgesBySource[currentId])
            {
                if (!visited.Add(edge.TargetId))
                {
                    continue;
                }

                soFar.Add(edge);

                if (ends.Contains(edge.TargetId))
                {
                    paths.Add([.. soFar]);
                }
                else
                {
                    Walk(edge.TargetId, soFar, visited);
                }

                soFar.RemoveAt(soFar.Count - 1);
                visited.Remove(edge.TargetId);

                if (paths.Count >= MaxRenderedPaths)
                {
                    return;
                }
            }
        }
    }

    private static string RenderPath(CodeGraph.Graph.CodeGraph graph, List<Relationship> path)
    {
        var text = new StringBuilder();

        var first = graph.TryGetCodeElement(path[0].SourceId);
        text.Append(first?.FullName ?? path[0].SourceId);

        foreach (var edge in path)
        {
            var next = graph.TryGetCodeElement(edge.TargetId);
            text.Append("  --").Append(edge.Type).Append("-->  ");
            text.Append(next?.FullName ?? edge.TargetId);
        }

        return text.ToString();
    }
}
