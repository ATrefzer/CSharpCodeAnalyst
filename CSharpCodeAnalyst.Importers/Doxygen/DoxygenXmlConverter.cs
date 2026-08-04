using System.IO;
using System.Text;
using System.Xml.Linq;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Importers.Doxygen;

/// <summary>
///     Converts the XML output of doxygen (GENERATE_XML = YES) into a CodeGraph.
///     Mapping:
///     - One artificial Assembly element is the root of everything.
///     - Namespace-less elements go into the artificial "global" namespace below the assembly,
///     following the convention of the C# parser (see CodeElement.GlobalNamespaceName).
///     - Compounds: class/interface/struct/union/enum become type elements, namespaces become
///     Namespace elements. The hierarchy is derived from the qualified compound name
///     ("a::b::Outer::Inner"), template arguments are kept out of the splitting.
///     An "enum" compound only occurs for Java, where an enum is a type with its own members;
///     a C++ enum is a memberdef of its scope and is handled as a member below.
///     With <see cref="DoxygenHierarchyMode.Directories" /> the namespaces come from the directory
///     of each element's source file instead - see <see cref="ResolveDirectoryNamespace" />.
///     - Members: function -> Method, variable -> Field, enum -> Enum, property -> Property,
///     event -> Event. Everything else (typedefs, defines, friends) is skipped.
///     - Relationships: basecompoundref -> Inherits (Implements when the base is an interface),
///     "references" entries (REFERENCES_RELATION = YES) -> Calls when the target is a method,
///     Uses otherwise. Type references in signatures (return type, parameters, field types)
///     -> Uses.
///     Unresolved refids (external/system code) are skipped, so the graph stays self-contained.
///     Namespaces left without any content are dropped again (see
///     <see cref="RemoveEmptyNamespaces" />).
/// </summary>
public class DoxygenXmlConverter
{
    private static readonly HashSet<string> TypeKinds = ["class", "struct", "union", "interface", "enum"];

    private static readonly Dictionary<string, CodeElementType> MemberKindMap = new()
    {
        ["function"] = CodeElementType.Method,
        ["variable"] = CodeElementType.Field,
        ["enum"] = CodeElementType.Enum,
        ["property"] = CodeElementType.Property,
        ["event"] = CodeElementType.Event
    };

    private readonly Dictionary<string, CodeElement> _elementsById = new();
    private readonly DoxygenHierarchyMode _hierarchyMode;

    /// <summary>
    ///     Keyed by the path the namespace was built from: the C++ scope ("a::b") in
    ///     <see cref="DoxygenHierarchyMode.Declared" />, the relative directory in
    ///     <see cref="DoxygenHierarchyMode.Directories" />. Only one of the two is ever filled.
    /// </summary>
    private readonly Dictionary<string, CodeElement> _namespacesByPath = new();

    private readonly HashSet<(string SourceId, string TargetId, RelationshipType Type)> _relationships = [];

    /// <summary>Absolute path of the imported directory, the root the namespace paths start at.</summary>
    private readonly string? _sourceDirectory;

    private readonly Dictionary<string, CodeElement> _typesByCppName = new();

    private CodeElement _assembly = null!;
    private CodeElement? _globalNamespace;
    private int _nextSyntheticId = 1;

    /// <param name="hierarchyMode">Where the namespaces come from.</param>
    /// <param name="sourceDirectory">
    ///     The imported directory. Required for <see cref="DoxygenHierarchyMode.Directories" />,
    ///     ignored otherwise.
    /// </param>
    public DoxygenXmlConverter(DoxygenHierarchyMode hierarchyMode = DoxygenHierarchyMode.Declared, string? sourceDirectory = null)
    {
        if (hierarchyMode == DoxygenHierarchyMode.Directories && string.IsNullOrWhiteSpace(sourceDirectory))
        {
            throw new ArgumentException("A source directory is required to derive the hierarchy from directories.", nameof(sourceDirectory));
        }

        _hierarchyMode = hierarchyMode;
        _sourceDirectory = sourceDirectory is null ? null : Path.GetFullPath(sourceDirectory);
    }

    public int SkippedUnresolvedReferences { get; private set; }

    public CodeGraph.Graph.CodeGraph Convert(string xmlDirectory, string assemblyName)
    {
        var compounds = LoadCompounds(xmlDirectory);

        _assembly = new CodeElement("assembly", CodeElementType.Assembly, assemblyName, assemblyName, null);
        _elementsById[_assembly.Id] = _assembly;

        // Parents must exist before their children, so order by nesting depth.
        var namespaces = compounds.Where(c => c.Kind == "namespace").OrderBy(c => SplitQualifiedName(c.QualifiedName).Count).ToList();
        var types = compounds.Where(c => TypeKinds.Contains(c.Kind)).OrderBy(c => SplitQualifiedName(c.QualifiedName).Count).ToList();
        var files = compounds.Where(c => c.Kind == "file").ToList();

        if (_hierarchyMode == DoxygenHierarchyMode.Declared)
        {
            foreach (var ns in namespaces)
            {
                EnsureNamespaceChain(SplitQualifiedName(ns.QualifiedName), ns.RefId);
            }
        }

        foreach (var type in types)
        {
            CreateType(type);
        }

        // Members of types and namespaces first. File compounds repeat some of those
        // memberdefs under the same id, so afterwards only the true global-scope members
        // are left for the artificial "global" namespace.
        foreach (var type in types)
        {
            CreateMembers(type.Definition, _elementsById[type.RefId]);
        }

        // Free functions and variables. In directory mode the namespace compounds produced no
        // element to hang them on, so each one goes to the directory of its own location - the
        // same rule the file compounds below follow.
        foreach (var ns in namespaces)
        {
            CreateMembers(ns.Definition, _hierarchyMode == DoxygenHierarchyMode.Declared ? _elementsById[ns.RefId] : null);
        }

        foreach (var file in files)
        {
            CreateMembers(file.Definition, _hierarchyMode == DoxygenHierarchyMode.Declared ? GetGlobalNamespace() : null);
        }

        foreach (var type in types)
        {
            AddInheritance(type);
        }

        foreach (var compound in types.Concat(namespaces).Concat(files))
        {
            AddMemberRelationships(compound.Definition);
        }

        RemoveEmptyNamespaces();

        return new CodeGraph.Graph.CodeGraph { Nodes = new Dictionary<string, CodeElement>(_elementsById) };
    }

    private static List<CompoundInfo> LoadCompounds(string xmlDirectory)
    {
        var indexPath = Path.Combine(xmlDirectory, "index.xml");
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException("index.xml not found - is this a doxygen XML output directory (GENERATE_XML = YES)?", indexPath);
        }

        var wantedKinds = new HashSet<string>(TypeKinds) { "namespace", "file" };
        var result = new List<CompoundInfo>();

        var index = XDocument.Load(indexPath);
        foreach (var compound in index.Root!.Elements("compound"))
        {
            var kind = (string?)compound.Attribute("kind") ?? string.Empty;
            if (!wantedKinds.Contains(kind))
            {
                continue;
            }

            var refId = (string?)compound.Attribute("refid");
            if (refId is null)
            {
                continue;
            }

            var compoundPath = Path.Combine(xmlDirectory, refId + ".xml");
            if (!File.Exists(compoundPath))
            {
                continue;
            }

            // Fixed invalid character when loading xml 
            var xmlContent = File.ReadAllText(compoundPath, Encoding.UTF8);
            xmlContent = xmlContent.TrimStart('\uFEFF'); // Remove BOM
            var document = XDocument.Parse(xmlContent);
            //var document = XDocument.Load(compoundPath);

            foreach (var compoundDef in document.Root!.Elements("compounddef"))
            {
                var defKind = (string?)compoundDef.Attribute("kind") ?? kind;
                var id = (string?)compoundDef.Attribute("id");
                if (id is null || !wantedKinds.Contains(defKind))
                {
                    continue;
                }

                var qualifiedName = Sanitize(compoundDef.Element("compoundname")?.Value ?? id);
                result.Add(new CompoundInfo(id, defKind, qualifiedName, compoundDef));
            }
        }

        return result;
    }

    /// <summary>
    ///     Drops namespaces that ended up without any content. Two sources produce them:
    ///     doxygen invents namespace compounds for scopes it only saw in a reference (Java code
    ///     using java.util yields an empty "java::util"), and the artificial "global" namespace is
    ///     created for every file compound even when the file has no global-scope members.
    ///     Neither carries information, but both show up in the tree as an empty package.
    ///     Deepest first, so a parent that only held such children is dropped in the same pass.
    /// </summary>
    private void RemoveEmptyNamespaces()
    {
        var referencedIds = _relationships.Select(r => r.TargetId).ToHashSet();

        var candidates = _elementsById.Values
            .Where(e => e.ElementType == CodeElementType.Namespace)
            .OrderByDescending(GetDepth)
            .ToList();

        foreach (var element in candidates)
        {
            if (element.Children.Count > 0 || element.Relationships.Count > 0 || referencedIds.Contains(element.Id))
            {
                continue;
            }

            element.Parent?.Children.Remove(element);
            _elementsById.Remove(element.Id);
        }

        return;

        static int GetDepth(CodeElement element)
        {
            var depth = 0;
            for (var current = element.Parent; current is not null; current = current.Parent)
            {
                depth++;
            }

            return depth;
        }
    }

    private CodeElement GetGlobalNamespace()
    {
        if (_globalNamespace is null)
        {
            _globalNamespace = new CodeElement("global-namespace", CodeElementType.Namespace,
                CodeElement.GlobalNamespaceName, _assembly.FullName + "." + CodeElement.GlobalNamespaceName, _assembly);
            _assembly.Children.Add(_globalNamespace);
            _elementsById[_globalNamespace.Id] = _globalNamespace;
        }

        return _globalNamespace;
    }

    /// <summary>
    ///     Creates the namespace elements for the given path ("a::b::c" or "src/widgets" split into
    ///     parts) below the assembly, reusing existing ones. The doxygen refid is used as element id
    ///     for the last segment when this call is made for the namespace's own compound.
    /// </summary>
    private CodeElement EnsureNamespaceChain(List<string> parts, string? refIdForLast = null)
    {
        var parent = _assembly;
        var path = string.Empty;
        for (var i = 0; i < parts.Count; i++)
        {
            path = path.Length == 0 ? parts[i] : path + "::" + parts[i];
            if (_namespacesByPath.TryGetValue(path, out var existing))
            {
                parent = existing;
                continue;
            }

            var id = i == parts.Count - 1 && refIdForLast is not null ? refIdForLast : $"ns_{_nextSyntheticId++}";
            var element = new CodeElement(id, CodeElementType.Namespace, parts[i], parent.FullName + "." + parts[i], parent);
            parent.Children.Add(element);
            _elementsById[id] = element;
            _namespacesByPath[path] = element;
            parent = element;
        }

        return parent;
    }

    /// <summary>
    ///     The namespace for an element in <see cref="DoxygenHierarchyMode.Directories" />: the
    ///     directory of its source file, relative to the imported directory, one namespace per
    ///     path segment. The file name itself is not a segment - a header and its implementation
    ///     belong together. Everything the imported directory does not contain (a file with no
    ///     location, a header pulled in from elsewhere) goes to the artificial "global" namespace,
    ///     just like a file directly in the imported directory.
    /// </summary>
    private CodeElement ResolveDirectoryNamespace(XElement? location)
    {
        var segments = GetDirectorySegments(location);
        return segments.Count == 0 ? GetGlobalNamespace() : EnsureNamespaceChain(segments);
    }

    private List<string> GetDirectorySegments(XElement? location)
    {
        var file = ToSystemPath((string?)location?.Attribute("file"));
        if (file is null || _sourceDirectory is null)
        {
            return [];
        }

        string relative;
        try
        {
            // doxygen reports absolute paths; the base path only matters for a relative one.
            relative = Path.GetRelativePath(_sourceDirectory, Path.GetFullPath(file, _sourceDirectory));
        }
        catch (ArgumentException)
        {
            return [];
        }

        var directory = Path.GetDirectoryName(relative);
        if (string.IsNullOrEmpty(directory))
        {
            return [];
        }

        // Outside the imported directory: another drive keeps the path rooted, a parent
        // directory produces "..".
        if (Path.IsPathRooted(directory) || directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
        {
            return [];
        }

        return directory
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != ".")
            .Select(Sanitize)
            .Where(segment => segment.Length > 0)
            .ToList();
    }

    private void CreateType(CompoundInfo compound)
    {
        var parts = SplitQualifiedName(compound.QualifiedName);
        var name = parts[^1];
        var prefixParts = parts[..^1];
        var prefix = string.Join("::", prefixParts);

        // A nested type stays below its outer type in both modes - it is part of that type, not
        // of a folder. Types are processed outer-before-inner, so an outer type is known here.
        CodeElement parent;
        if (prefixParts.Count > 0 && _typesByCppName.TryGetValue(prefix, out var outerType))
        {
            parent = outerType;
        }
        else if (_hierarchyMode == DoxygenHierarchyMode.Directories)
        {
            parent = ResolveDirectoryNamespace(compound.Definition.Element("location"));
        }
        else if (prefixParts.Count == 0)
        {
            parent = GetGlobalNamespace();
        }
        else if (_namespacesByPath.TryGetValue(prefix, out var ns))
        {
            parent = ns;
        }
        else
        {
            parent = EnsureNamespaceChain(prefixParts);
        }

        var elementType = compound.Kind switch
        {
            "interface" => CodeElementType.Interface,
            "struct" or "union" => CodeElementType.Struct,
            "enum" => CodeElementType.Enum,
            _ => CodeElementType.Class
        };

        var element = new CodeElement(compound.RefId, elementType, name, parent.FullName + "." + name, parent);
        parent.Children.Add(element);
        AddLocation(element, compound.Definition.Element("location"));
        _elementsById[compound.RefId] = element;
        _typesByCppName[compound.QualifiedName] = element;
    }

    /// <param name="parent">
    ///     The element all members belong to, or null to resolve it per member from its own
    ///     location (directory mode, where a namespace or file compound has no element of its own).
    /// </param>
    private void CreateMembers(XElement compoundDef, CodeElement? parent)
    {
        foreach (var memberDef in compoundDef.Elements("sectiondef").Elements("memberdef"))
        {
            var id = (string?)memberDef.Attribute("id");
            if (id is null || _elementsById.ContainsKey(id))
            {
                continue;
            }

            var kind = (string?)memberDef.Attribute("kind") ?? string.Empty;
            if (!MemberKindMap.TryGetValue(kind, out var elementType))
            {
                continue;
            }

            var name = Sanitize(memberDef.Element("name")?.Value ?? string.Empty);
            if (name.Length == 0)
            {
                name = "unnamed";
            }

            var location = memberDef.Element("location");
            var memberParent = parent ?? ResolveDirectoryNamespace(location);

            var element = new CodeElement(id, elementType, name, memberParent.FullName + "." + name, memberParent);
            memberParent.Children.Add(element);
            AddLocation(element, location);
            _elementsById[id] = element;
        }
    }

    private void AddInheritance(CompoundInfo compound)
    {
        var derived = _elementsById[compound.RefId];
        foreach (var baseRef in compound.Definition.Elements("basecompoundref"))
        {
            var refId = (string?)baseRef.Attribute("refid");
            if (refId is null || !_elementsById.TryGetValue(refId, out var baseElement))
            {
                // External base class (std::, third party) - not part of the graph.
                SkippedUnresolvedReferences++;
                continue;
            }

            var relationshipType = baseElement.ElementType == CodeElementType.Interface
                ? RelationshipType.Implements
                : RelationshipType.Inherits;
            AddRelationship(derived, baseElement, relationshipType);
        }
    }

    private void AddMemberRelationships(XElement compoundDef)
    {
        foreach (var memberDef in compoundDef.Elements("sectiondef").Elements("memberdef"))
        {
            var id = (string?)memberDef.Attribute("id");
            if (id is null || !_elementsById.TryGetValue(id, out var source))
            {
                continue;
            }

            // REFERENCES_RELATION = YES: everything this member's body refers to.
            foreach (var reference in memberDef.Elements("references"))
            {
                var refId = (string?)reference.Attribute("refid");
                if (refId is null || !_elementsById.TryGetValue(refId, out var target))
                {
                    SkippedUnresolvedReferences++;
                    continue;
                }

                var relationshipType = target.ElementType == CodeElementType.Method
                    ? RelationshipType.Calls
                    : RelationshipType.Uses;
                AddRelationship(source, target, relationshipType);
            }

            // Types in the signature: return type / field type and parameter types.
            var typeRefs = memberDef.Elements("type")
                .Concat(memberDef.Elements("param").Elements("type"))
                .SelectMany(t => t.Descendants("ref"));
            foreach (var typeRef in typeRefs)
            {
                var refId = (string?)typeRef.Attribute("refid");
                if (refId is null || !_elementsById.TryGetValue(refId, out var target))
                {
                    continue;
                }

                if (target.ElementType is CodeElementType.Class or CodeElementType.Struct or CodeElementType.Interface or CodeElementType.Enum)
                {
                    AddRelationship(source, target, RelationshipType.Uses);
                }
            }
        }
    }

    private void AddRelationship(CodeElement source, CodeElement target, RelationshipType type)
    {
        if (source.Id == target.Id || !_relationships.Add((source.Id, target.Id, type)))
        {
            return;
        }

        source.Relationships.Add(new Relationship(source.Id, target.Id, type));
    }

    private static void AddLocation(CodeElement element, XElement? location)
    {
        if (location is null)
        {
            return;
        }

        var file = (string?)location?.Attribute("file");
        file = ToSystemPath(file);
        if (file is null)
        {
            return;
        }

        var line = (int?)location?.Attribute("line") ?? 0;
        var column = (int?)location?.Attribute("column") ?? 0;
        element.SourceLocations.Add(new SourceLocation(file, line, column));
    }

    private static string? ToSystemPath(string? path)
    {
        return path?.Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    ///     Splits a qualified C++ name on "::" while ignoring separators inside template
    ///     argument lists ("a::Foo&lt;std::string&gt;" is "a" + "Foo&lt;std::string&gt;").
    /// </summary>
    private static List<string> SplitQualifiedName(string qualifiedName)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < qualifiedName.Length; i++)
        {
            var c = qualifiedName[i];
            if (c == '<')
            {
                depth++;
            }
            else if (c == '>')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0 && c == ':' && i + 1 < qualifiedName.Length && qualifiedName[i + 1] == ':')
            {
                parts.Add(qualifiedName[start..i]);
                i++;
                start = i + 1;
            }
        }

        parts.Add(qualifiedName[start..]);
        return parts;
    }

    /// <summary>
    ///     Keep names free of whitespace ("operator ==" -> "operator==", "Foo&lt; T &gt;" -> "Foo&lt;T&gt;"),
    ///     so they survive every whitespace-splitting consumer (e.g. the plain text graph format).
    /// </summary>
    private static string Sanitize(string name)
    {
        return string.Concat(name.Where(c => !char.IsWhiteSpace(c)));
    }

    private sealed record CompoundInfo(string RefId, string Kind, string QualifiedName, XElement Definition);
}