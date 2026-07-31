using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeParser.Xaml;

/// <summary>One analyzed project: its assembly element in the graph and the directory to scan for XAML.</summary>
public sealed record XamlProject(CodeElement Assembly, string Directory);

/// <summary>
///     Turns the references <see cref="XamlReferenceExtractor" /> finds into real relationships in the code
///     graph, so a type that is only ever instantiated from XAML no longer looks unused.
///     <para>
///         The source of such a relationship is the code-behind class named by <c>x:Class</c>. A resource
///         dictionary has no code-behind, so a synthetic class named after the file takes its place -
///         the same device the parser already uses for top-level statements ("GlobalStatements"). Those
///         synthetic elements have no incoming references of their own (nothing resolves the
///         <c>MergedDictionaries</c> URIs), so they show up in a dead code analysis. That is a known and
///         accepted cost; there were 14 of them in this repository against 1050 findings.
///     </para>
///     <para>
///         Resolution is by exact name, never by guessing: the xmlns gives the CLR namespace and optionally
///         the assembly. Without <c>;assembly=</c> XAML means the assembly the file is compiled into, which
///         is what is tried first; a unique match elsewhere is accepted as a fallback.
///     </para>
/// </summary>
public static class XamlGraphLinker
{
    /// <summary>The element name the parser gives a constructor (it comes straight from the symbol).</summary>
    private const string ConstructorName = ".ctor";

    public static int Link(CodeGraph.Graph.CodeGraph graph, IReadOnlyList<XamlProject> projects)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(projects);

        var typesByAssembly = BuildTypeLookup(graph);
        var added = 0;

        foreach (var project in projects)
        {
            foreach (var file in EnumerateXamlFiles(project.Directory))
            {
                added += LinkFile(graph, project, file, typesByAssembly);
            }
        }

        return added;
    }

    private static int LinkFile(CodeGraph.Graph.CodeGraph graph, XamlProject project, string file,
        Dictionary<string, Dictionary<string, CodeElement>> typesByAssembly)
    {
        XamlFileReferences references;
        try
        {
            references = XamlReferenceExtractor.Extract(File.ReadAllText(file));
        }
        catch (IOException)
        {
            // An unreadable file must not break the parse run.
            return 0;
        }

        if (references.References.Count == 0)
        {
            return 0;
        }

        var source = ResolveSource(graph, project, file, references, typesByAssembly);
        var added = 0;

        foreach (var reference in references.References)
        {
            foreach (var target in ResolveTargets(project, reference, typesByAssembly))
            {
                if (target.Id == source.Id)
                {
                    continue;
                }

                if (AddReference(source, target, file, reference))
                {
                    added++;
                }
            }
        }

        return added;
    }

    /// <summary>
    ///     Adds the relationship, or merges the location into the existing one. The relationship set is
    ///     keyed by (source, target, type), so a plain Add would silently drop the new source location.
    /// </summary>
    private static bool AddReference(CodeElement source, CodeElement target, string file, XamlReference reference)
    {
        var location = new SourceLocation(file, reference.Line, reference.Column);
        var existing = source.Relationships.FirstOrDefault(
            r => r.TargetId == target.Id && r.Type == RelationshipType.Uses);

        if (existing is not null)
        {
            if (!existing.SourceLocations.Contains(location))
            {
                existing.SourceLocations.Add(location);
            }

            existing.SetAttribute(RelationshipAttribute.IsXamlReference);
            return false;
        }

        var relationship = new Relationship(source.Id, target.Id, RelationshipType.Uses,
            RelationshipAttribute.IsXamlReference);
        relationship.SourceLocations.Add(location);
        source.Relationships.Add(relationship);
        return true;
    }

    private static CodeElement ResolveSource(CodeGraph.Graph.CodeGraph graph, XamlProject project, string file,
        XamlFileReferences references, Dictionary<string, Dictionary<string, CodeElement>> typesByAssembly)
    {
        if (references.CodeBehindClass is not null &&
            typesByAssembly.TryGetValue(project.Assembly.Name, out var types) &&
            types.TryGetValue(references.CodeBehindClass, out var codeBehind))
        {
            return codeBehind;
        }

        return GetOrCreateSyntheticElement(graph, project, file);
    }

    /// <summary>
    ///     The stand-in for a XAML file that has no code-behind class. Named after the path relative to the
    ///     project so two files with the same name stay distinguishable.
    /// </summary>
    private static CodeElement GetOrCreateSyntheticElement(CodeGraph.Graph.CodeGraph graph, XamlProject project,
        string file)
    {
        var name = Path.ChangeExtension(Path.GetRelativePath(project.Directory, file), null)
            .Replace(Path.DirectorySeparatorChar, '.')
            .Replace(Path.AltDirectorySeparatorChar, '.');

        var fullName = project.Assembly.FullName + "." + name;

        var existing = project.Assembly.Children.FirstOrDefault(c => c.FullName == fullName);
        if (existing is not null)
        {
            return existing;
        }

        var element = new CodeElement(Guid.NewGuid().ToString(), CodeElementType.Class, name, fullName,
            project.Assembly);
        element.SourceLocations.Add(new SourceLocation(file, 1, 1));

        project.Assembly.Children.Add(element);
        graph.Nodes[element.Id] = element;
        return element;
    }

    private static IEnumerable<CodeElement> ResolveTargets(XamlProject project, XamlReference reference,
        Dictionary<string, Dictionary<string, CodeElement>> typesByAssembly)
    {
        var type = ResolveType(project, reference, typesByAssembly);
        if (type is null)
        {
            yield break;
        }

        if (reference.MemberName is not null)
        {
            // {x:Static Type.Member} - prefer the member, fall back to the type when it has no element
            // (e.g. an enum value or a member the parser did not model).
            yield return type.Children.FirstOrDefault(c => c.Name == reference.MemberName) ?? type;
            yield break;
        }

        yield return type;

        if (!reference.IsInstantiation)
        {
            yield break;
        }

        // An object element runs the constructor. Without this edge the constructor has no incoming
        // reference at all, and everything only it calls dies with it in the cascade - the body of a
        // XAML-instantiated control lives almost entirely below its constructor.
        // Overloads share the element name, so all of them are linked: XAML picks the parameterless one,
        // but the graph cannot tell them apart, and an edge too many is far cheaper here than a missing
        // one.
        foreach (var constructor in type.Children.Where(IsConstructor))
        {
            yield return constructor;
        }
    }

    private static bool IsConstructor(CodeElement element)
    {
        return element is { ElementType: CodeElementType.Method, Name: ConstructorName };
    }

    private static CodeElement? ResolveType(XamlProject project, XamlReference reference,
        Dictionary<string, Dictionary<string, CodeElement>> typesByAssembly)
    {
        // An explicit ";assembly=" wins; without it XAML means the assembly the file is compiled into.
        var assemblyName = reference.AssemblyName ?? project.Assembly.Name;
        if (typesByAssembly.TryGetValue(assemblyName, out var types) &&
            types.TryGetValue(reference.TypeFullName, out var declared))
        {
            return declared;
        }

        // Fallback: a unique match anywhere. Ambiguous names are dropped rather than guessed.
        var matches = typesByAssembly.Values
            .Select(candidates => candidates.GetValueOrDefault(reference.TypeFullName))
            .Where(candidate => candidate is not null)
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    ///     Maps assembly name -&gt; CLR full name ("Namespace.Type") -&gt; type element. The assembly node and
    ///     the synthetic global namespace are not part of a CLR name and are skipped.
    /// </summary>
    private static Dictionary<string, Dictionary<string, CodeElement>> BuildTypeLookup(
        CodeGraph.Graph.CodeGraph graph)
    {
        var lookup = new Dictionary<string, Dictionary<string, CodeElement>>();

        foreach (var element in graph.Nodes.Values)
        {
            if (!element.IsType() || element.IsExternal)
            {
                continue;
            }

            var path = element.GetPathToRoot(true);
            if (path.Count < 2 || path[0].ElementType != CodeElementType.Assembly)
            {
                continue;
            }

            var segments = path.Skip(1)
                .Where(p => p.ElementType != CodeElementType.Namespace ||
                            p.Name != CodeElement.GlobalNamespaceName)
                .Select(p => p.Name);

            var types = lookup.TryGetValue(path[0].Name, out var existing) ? existing : lookup[path[0].Name] = [];

            // A name collision would mean two types with the same full name in one assembly, which the
            // compiler would already have rejected.
            types[string.Join(".", segments)] = element;
        }

        return lookup;
    }

    private static IEnumerable<string> EnumerateXamlFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }
}
