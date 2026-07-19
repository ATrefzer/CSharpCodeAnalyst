using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public static class PatternMatcher
{
    /// <summary>
    ///     Resolves a pattern like "Business.**" to a set of CodeElement IDs
    ///     Pattern format: "Base.Path" or "Base.Path.*" or "Base.Path.**"
    /// </summary>
    public static HashSet<string> ResolvePattern(string pattern, CodeGraph.Graph.CodeGraph codeGraph)
    {
        var matchingIds = new HashSet<string>();

        if (string.IsNullOrEmpty(pattern))
            return matchingIds;

        // Split pattern into base path and wildcard suffix
        var (basePath, expansionMode) = ParsePattern(pattern);

        // A full path is not necessarily unique: overloaded methods share the same full name.
        // Resolve to ALL matching elements and union their expansions, so e.g. an ALLOW rule for
        // a method covers every overload instead of an arbitrary single one.
        foreach (var startElement in FindStartElements(basePath, codeGraph))
        {
            ApplyExpansion(startElement, expansionMode, matchingIds);
        }

        return matchingIds;
    }

    /// <summary>
    ///     Resolves a plain path (no wildcard) to the matching elements and all their descendants,
    ///     i.e. "X" behaves like "X.**". For rules that always mean the whole subtree (NOCYCLES).
    /// </summary>
    public static HashSet<string> ResolveSubtree(string path, CodeGraph.Graph.CodeGraph codeGraph)
    {
        var matchingIds = new HashSet<string>();
        foreach (var startElement in FindStartElements(path, codeGraph))
        {
            ApplyExpansion(startElement, ExpansionMode.Recursive, matchingIds);
        }

        return matchingIds;
    }

    private static (string basePath, ExpansionMode mode) ParsePattern(string pattern)
    {
        if (pattern.EndsWith(".**"))
        {
            var basePath = pattern.Substring(0, pattern.Length - 3); // Remove ".**"
            return (basePath, ExpansionMode.Recursive);
        }

        if (pattern.EndsWith(".*"))
        {
            var basePath = pattern.Substring(0, pattern.Length - 2); // Remove ".*"
            return (basePath, ExpansionMode.DirectChildren);
        }

        // No wildcard suffix - just the element itself
        return (pattern, ExpansionMode.Self);
    }

    /// <summary>
    ///     Finds the elements a pattern anchors on. The written path is matched exactly first; only when
    ///     that matches nothing is the equivalent path with the "global" namespace segment toggled tried
    ///     (see <see cref="ToggleGlobalNamespace" />). Exact-first means a rule can never match more than
    ///     what it says, and the fallback only ever helps a rule that would otherwise be dead.
    /// </summary>
    private static List<CodeElement> FindStartElements(string basePath, CodeGraph.Graph.CodeGraph codeGraph)
    {
        var matches = FindByFullName(basePath, codeGraph);
        if (matches.Count > 0)
        {
            return matches;
        }

        var alternativePath = ToggleGlobalNamespace(basePath, codeGraph);
        return alternativePath is null ? matches : FindByFullName(alternativePath, codeGraph);
    }

    private static List<CodeElement> FindByFullName(string path, CodeGraph.Graph.CodeGraph codeGraph)
    {
        // All elements with an exact FullName match (more than one for overloaded members).
        // The comparison is case-sensitive: C# identifiers are, so "MyApp.business" must not match
        // "MyApp.Business" - otherwise a typo silently hits the wrong element instead of raising the
        // no-match warning that is supposed to catch it.
        return codeGraph.Nodes.Values
            .Where(element => string.Equals(element.FullName, path, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    ///     Returns the same path with the synthetic "global" namespace segment added or removed, or null
    ///     when the path names no assembly of this graph (nothing to toggle then).
    ///     <para>
    ///         The parser inserts a "global" namespace below every assembly as soon as *any* assembly
    ///         holds code outside a namespace. A rule file must not break over that,
    ///         so both spellings resolve to the same element.
    ///     </para>
    ///     <para>
    ///         The assembly is taken from the graph rather than by splitting off the first segment,
    ///         because assembly names contain dots themselves ("MyApp.Business.dll" -> "MyApp.Business").
    ///     </para>
    /// </summary>
    private static string? ToggleGlobalNamespace(string basePath, CodeGraph.Graph.CodeGraph codeGraph)
    {
        const string globalSegment = CodeElement.GlobalNamespaceName + ".";

        foreach (var assembly in codeGraph.GetRoots().Where(r => r.ElementType == CodeElementType.Assembly))
        {
            var prefix = assembly.FullName + ".";
            if (!basePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = basePath[prefix.Length..];

            // "MyApp.global.Business" -> "MyApp.Business"
            if (relativePath.StartsWith(globalSegment, StringComparison.Ordinal))
            {
                return prefix + relativePath[globalSegment.Length..];
            }

            // "MyApp.global" alone means the namespace itself; without it the assembly is the container.
            if (relativePath == CodeElement.GlobalNamespaceName)
            {
                return assembly.FullName;
            }

            // "MyApp.Business" -> "MyApp.global.Business"
            return prefix + globalSegment + relativePath;
        }

        return null;
    }

    private static void ApplyExpansion(CodeElement startElement, ExpansionMode mode, HashSet<string> matchingIds)
    {
        switch (mode)
        {
            case ExpansionMode.Self:
                // Only the start element itself
                matchingIds.Add(startElement.Id);
                break;

            case ExpansionMode.DirectChildren:
                // Start element + direct children only
                matchingIds.Add(startElement.Id);
                foreach (var child in startElement.Children)
                {
                    matchingIds.Add(child.Id);
                }

                break;

            case ExpansionMode.Recursive:
                // Start element + all descendants recursively
                var allDescendants = startElement.GetChildrenIncludingSelf();
                foreach (var descendant in allDescendants)
                {
                    matchingIds.Add(descendant);
                }

                break;
        }
    }

    private enum ExpansionMode
    {
        Self, // No wildcard - just the element itself
        DirectChildren, // .* - element + direct children
        Recursive // .** - element + all descendants
    }
}