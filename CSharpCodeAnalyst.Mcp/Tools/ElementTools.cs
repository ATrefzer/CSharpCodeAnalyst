using System.ComponentModel;
using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Search;
using CSharpCodeAnalyst.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     Tools that work on a single code element.
/// </summary>
[McpServerToolType]
public sealed class ElementTools(ICodeGraphSnapshotSource snapshotSource)
{
    /// <summary>
    ///     A type with many members still fits; a namespace with hundreds of types does not, and listing
    ///     them all would bury the rest of the answer.
    /// </summary>
    private const int ChildLimit = 40;

    private const int LocationLimit = 5;

    private const int DefaultSearchLimit = 50;

    /// <summary>
    ///     A caller that asks for a thousand hits does not want to read them - it wants to be sure it
    ///     saw everything, and a truncation notice answers that better than a flooded context window.
    /// </summary>
    private const int MaxSearchLimit = 200;

    [McpServerTool(Name = "search_elements", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Finds code elements by name. This is the entry point for every other tool, because element " +
        "ids cannot be guessed.\n" +
        "Query syntax. An all-lowercase term matches anywhere in the full name, case-insensitively. " +
        "A term containing an uppercase letter switches to camel-hump matching: it is split at every " +
        "uppercase letter and the parts must occur in that order, each starting a word, matched " +
        "case-sensitively. So 'OS', 'OrdServ' and 'OrderService' all find 'OrderService', but 'OSvc' " +
        "finds nothing, because 'Svc' does not occur in the name.\n" +
        "Space means AND, '|' means OR, a leading '-' excludes. 'type:class' (also interface, struct, " +
        "record, method, property, field, event, enum, delegate, namespace, assembly) restricts the " +
        "kind; 'source:extern', 'source:intern' and 'source:generated' restrict the origin.\n" +
        "Example: 'order type:class -source:extern' finds classes in the analyzed code whose full " +
        "name contains 'order'.")]
    public async Task<string> SearchElementsAsync(
        [Description("Search expression, see the syntax above.")]
        string query,
        [Description("Maximum number of results (default 50, capped at 200).")]
        int limit = DefaultSearchLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "The query is empty. Pass a name or a search expression.";
        }

        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var effectiveLimit = Math.Clamp(limit, 1, MaxSearchLimit);
        var expression = SearchExpressionFactory.CreateSearchExpression(query);

        var matches = snapshot.Graph.Nodes.Values
            .Where(element => expression.Evaluate(element))
            .OrderBy(element => Rank(element, query))
            .ThenBy(element => element.IsExternal)
            .ThenBy(element => element.ElementType)
            .ThenBy(element => element.FullName, StringComparer.Ordinal)
            .ToList();

        if (matches.Count == 0)
        {
            return $"Nothing matches '{query}'. Note that the search runs over the graph, not over " +
                   "your files: anything the parser did not see is not in it, and external code is " +
                   "only present as far as it is referenced.";
        }

        var text = new StringBuilder();
        text.Append(matches.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" match(es) for '").Append(query).Append("': ")
            .AppendLine(ElementFormatter.Summarize(matches, m => m.ElementType.ToString()));
        text.AppendLine();

        ElementFormatter.AppendLimited(text, matches, effectiveLimit);

        return text.ToString();
    }

    /// <summary>
    ///     Puts the element the caller most likely meant first. The expression itself does not rank -
    ///     it only says yes or no - so a search for "OrderService" would otherwise bury the type among
    ///     its own members and everything else whose full name contains the word.
    ///     <para>
    ///         Only meaningful when the query is a plain name. For an expression with operators nothing
    ///         matches exactly, everything lands in the last bucket, and the remaining sort keys decide.
    ///     </para>
    /// </summary>
    private static int Rank(CodeElement element, string query)
    {
        if (string.Equals(element.Name, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return element.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    [McpServerTool(Name = "describe_element", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Everything known about one code element: kind, full path, accessibility, where it is defined, " +
        "what it contains, and how many relationships run in and out. Takes an id from search_elements.")]
    public async Task<string> DescribeElementAsync(
        [Description("Element id, as returned by search_elements.")]
        string id,
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

        var text = new StringBuilder();

        text.Append('[').Append(element.ElementType).Append("] ").AppendLine(element.FullName);
        text.Append("id: ").AppendLine(element.Id);

        AppendFlags(text, element);
        AppendLocations(text, element);
        AppendParents(text, element);
        AppendChildren(text, element);
        AppendRelationships(text, snapshot.Graph, element);

        return text.ToString();
    }

    private static void AppendFlags(StringBuilder text, CodeElement element)
    {
        // Unknown is not a value, it means the producer told us nothing - reporting it as an access
        // level would invite exactly the conclusion the model must not draw.
        if (element.AccessLevel != AccessLevel.Unknown)
        {
            text.Append("Access: ").AppendLine(element.AccessLevel.ToString());
        }

        if (element.IsExternal)
        {
            text.AppendLine(
                "External: defined outside the analyzed code. Its own dependencies were not analyzed, " +
                "so an empty outgoing result says nothing about it.");
        }

        if (element.IsGenerated)
        {
            text.AppendLine("Generated: written by a tool. Editing it by hand has no lasting effect.");
        }

        if (element.Attributes.Count > 0)
        {
            text.Append("Attributes: ").AppendLine(string.Join(", ", element.Attributes.Order()));
        }
    }

    private static void AppendLocations(StringBuilder text, CodeElement element)
    {
        if (element.SourceLocations.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(element.SourceLocations.Count == 1 ? "Defined in:" : "Declarations:");

        foreach (var location in element.SourceLocations.Take(LocationLimit))
        {
            text.Append("  ").Append(location.File).Append(':')
                .AppendLine(location.Line.ToString(CultureInfo.InvariantCulture));
        }

        if (element.SourceLocations.Count > LocationLimit)
        {
            var omitted = element.SourceLocations.Count - LocationLimit;
            text.Append("  ... ").Append(omitted.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" more");
        }
    }

    private static void AppendParents(StringBuilder text, CodeElement element)
    {
        var path = element.GetPathToRoot(false);
        if (path.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("Contained in:");
        foreach (var ancestor in path)
        {
            text.Append("  ").AppendLine(ElementFormatter.Line(ancestor));
        }
    }

    private static void AppendChildren(StringBuilder text, CodeElement element)
    {
        if (element.Children.Count == 0)
        {
            return;
        }

        var children = element.Children
            .OrderBy(child => child.ElementType)
            .ThenBy(child => child.Name, StringComparer.Ordinal)
            .ToList();

        text.AppendLine();
        text.Append("Contains (").Append(children.Count.ToString(CultureInfo.InvariantCulture))
            .Append("): ")
            .AppendLine(ElementFormatter.Summarize(children, child => child.ElementType.ToString()));

        ElementFormatter.AppendLimited(text, children, ChildLimit);
    }

    private static void AppendRelationships(StringBuilder text, CodeGraph.Graph.CodeGraph graph,
        CodeElement element)
    {
        text.AppendLine();

        // Outgoing relationships live on the element itself. Incoming ones do not - the graph stores
        // every relationship on its source - so finding them means one pass over all of them. At the
        // scale of a solution that is cheap, and the number is worth having: it is the difference
        // between a leaf and something the whole code base leans on.
        var outgoing = element.Relationships;
        if (outgoing.Count > 0)
        {
            text.Append("Outgoing relationships (")
                .Append(outgoing.Count.ToString(CultureInfo.InvariantCulture)).Append("): ")
                .AppendLine(ElementFormatter.Summarize(outgoing, r => r.Type.ToString()));
        }
        else
        {
            text.AppendLine("Outgoing relationships: none");
        }

        var incoming = graph.GetAllRelationships().Count(r => r.TargetId == element.Id);
        text.Append("Incoming relationships: ")
            .AppendLine(incoming.ToString(CultureInfo.InvariantCulture));

        text.AppendLine(
            "Use find_outgoing_relationships or find_incoming_relationships for the actual entries.");
    }
}
