using System.IO;
using System.Xml.Linq;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Converts the XML output of doxygen (GENERATE_XML = YES) into a CodeGraph.
///     Mapping:
///     - One artificial Assembly element is the root of everything.
///     - Namespace-less elements go into the artificial "global" namespace below the assembly,
///     following the convention of the C# parser (see CodeElement.GlobalNamespaceName).
///     - Compounds: class/interface/struct/union become type elements, namespaces become
///     Namespace elements. The hierarchy is derived from the qualified compound name
///     ("a::b::Outer::Inner"), template arguments are kept out of the splitting.
///     - Members: function -> Method, variable -> Field, enum -> Enum, property -> Property,
///     event -> Event. Everything else (typedefs, defines, friends) is skipped.
///     - Relationships: basecompoundref -> Inherits (Implements when the base is an interface),
///     "references" entries (REFERENCES_RELATION = YES) -> Calls when the target is a method,
///     Uses otherwise. Type references in signatures (return type, parameters, field types)
///     -> Uses.
///     Unresolved refids (external/system code) are skipped, so the graph stays self-contained.
/// </summary>
public class DoxygenXmlConverter
{
    private static readonly HashSet<string> TypeKinds = ["class", "struct", "union", "interface"];

    private static readonly Dictionary<string, CodeElementType> MemberKindMap = new()
    {
        ["function"] = CodeElementType.Method,
        ["variable"] = CodeElementType.Field,
        ["enum"] = CodeElementType.Enum,
        ["property"] = CodeElementType.Property,
        ["event"] = CodeElementType.Event
    };

    private readonly Dictionary<string, CodeElement> _elementsById = new();
    private readonly Dictionary<string, CodeElement> _namespacesByCppName = new();
    private readonly HashSet<(string SourceId, string TargetId, RelationshipType Type)> _relationships = [];
    private readonly Dictionary<string, CodeElement> _typesByCppName = new();

    private CodeElement _assembly = null!;
    private CodeElement? _globalNamespace;
    private int _nextSyntheticId = 1;

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

        foreach (var ns in namespaces)
        {
            EnsureNamespaceChain(SplitQualifiedName(ns.QualifiedName), ns.RefId);
        }

        foreach (var type in types)
        {
            CreateType(type);
        }

        // Members of types and namespaces first. File compounds repeat some of those
        // memberdefs under the same id, so afterwards only the true global-scope members
        // are left for the artificial "global" namespace.
        foreach (var compound in types.Concat(namespaces))
        {
            CreateMembers(compound.Definition, _elementsById[compound.RefId]);
        }

        foreach (var file in files)
        {
            CreateMembers(file.Definition, GetGlobalNamespace());
        }

        foreach (var type in types)
        {
            AddInheritance(type);
        }

        foreach (var compound in types.Concat(namespaces).Concat(files))
        {
            AddMemberRelationships(compound.Definition);
        }

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

            var document = XDocument.Load(compoundPath);
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
    ///     Creates the namespace elements for the given path ("a::b::c" split into parts) below the
    ///     assembly, reusing existing ones. The doxygen refid is used as element id for the last
    ///     segment when this call is made for the namespace's own compound.
    /// </summary>
    private CodeElement EnsureNamespaceChain(List<string> parts, string? refIdForLast = null)
    {
        var parent = _assembly;
        var cppPath = string.Empty;
        for (var i = 0; i < parts.Count; i++)
        {
            cppPath = cppPath.Length == 0 ? parts[i] : cppPath + "::" + parts[i];
            if (_namespacesByCppName.TryGetValue(cppPath, out var existing))
            {
                parent = existing;
                continue;
            }

            var id = i == parts.Count - 1 && refIdForLast is not null ? refIdForLast : $"ns_{_nextSyntheticId++}";
            var element = new CodeElement(id, CodeElementType.Namespace, parts[i], parent.FullName + "." + parts[i], parent);
            parent.Children.Add(element);
            _elementsById[id] = element;
            _namespacesByCppName[cppPath] = element;
            parent = element;
        }

        return parent;
    }

    private void CreateType(CompoundInfo compound)
    {
        var parts = SplitQualifiedName(compound.QualifiedName);
        var name = parts[^1];

        CodeElement parent;
        if (parts.Count == 1)
        {
            parent = GetGlobalNamespace();
        }
        else
        {
            var prefixParts = parts[..^1];
            var prefix = string.Join("::", prefixParts);

            // The prefix is either an outer type (nested class) or a namespace. Types are
            // processed outer-before-inner, so an outer type is already known here.
            if (_typesByCppName.TryGetValue(prefix, out var outerType))
            {
                parent = outerType;
            }
            else if (_namespacesByCppName.TryGetValue(prefix, out var ns))
            {
                parent = ns;
            }
            else
            {
                parent = EnsureNamespaceChain(prefixParts);
            }
        }

        var elementType = compound.Kind switch
        {
            "interface" => CodeElementType.Interface,
            "struct" or "union" => CodeElementType.Struct,
            _ => CodeElementType.Class
        };

        var element = new CodeElement(compound.RefId, elementType, name, parent.FullName + "." + name, parent);
        parent.Children.Add(element);
        AddLocation(element, compound.Definition.Element("location"));
        _elementsById[compound.RefId] = element;
        _typesByCppName[compound.QualifiedName] = element;
    }

    private void CreateMembers(XElement compoundDef, CodeElement parent)
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

            var element = new CodeElement(id, elementType, name, parent.FullName + "." + name, parent);
            parent.Children.Add(element);
            AddLocation(element, memberDef.Element("location"));
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
        var file = (string?)location?.Attribute("file");
        if (location is null || file is null)
        {
            return;
        }

        var line = (int?)location.Attribute("line") ?? 0;
        var column = (int?)location.Attribute("column") ?? 0;
        element.SourceLocations.Add(new SourceLocation(file, line, column));
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