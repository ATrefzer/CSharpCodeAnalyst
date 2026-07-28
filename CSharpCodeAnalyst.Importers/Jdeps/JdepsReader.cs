using System.IO;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Importers.Jdeps;

/// <summary>
///     Reads the output of "jdeps -verbose:class", which is a flat list of
///     "from.Type -> to.Type module" lines.
///     Everything below a type is invisible in that format, and so is the kind of the dependency:
///     a base class shows up exactly like a parameter type. That is why every relationship here is
///     <see cref="RelationshipType.Uses" /> and every leaf a <see cref="CodeElementType.Class" /> -
///     not a shortcut, but all the input allows. A graph imported this way therefore has no
///     Inherits/Implements edges at all, and interfaces and enums are indistinguishable from
///     classes.
///     The doxygen based Java import (<see cref="Doxygen.DoxygenImporter" />) is the way to get
///     members, calls and inheritance; it reads the sources instead of the bytecode and pays for it
///     with name-based, incomplete resolution.
/// </summary>
public class JdepsReader
{
    private readonly Dictionary<string, CodeElement> _codeElements = new();
    private int _nextId = 1;

    public CodeGraph.Graph.CodeGraph ImportFromFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        return ImportFromLines(lines);
    }

    public CodeGraph.Graph.CodeGraph ImportFromLines(IEnumerable<string> lines)
    {
        _codeElements.Clear();
        _nextId = 1;

        // Parse all lines first
        foreach (var line in lines)
        {
            ParseLine(line.Trim());
        }


        var graph = new CodeGraph.Graph.CodeGraph
        {
            Nodes = _codeElements.ToDictionary(kvp => kvp.Value.Id, c => c.Value)
        };

        return graph;
    }

    private void ParseLine(string line)
    {
        line = line.Trim();
        if (!CanParseLine(line))
        {
            return;
        }

        var (fromClass, toClass) = ParseDependency(line);

        // Create code elements for both from and to
        var from = GetOrCreateCodeElementHierarchy(fromClass);
        var to = GetOrCreateCodeElementHierarchy(toClass);

        // Add dependency
        var relationship = new Relationship(from.Id, to.Id, RelationshipType.Uses);
        from.Relationships.Add(relationship);
    }

    private static (string fromClass, string toClass) ParseDependency(string line)
    {
        // Parse format: "from.class.Name -> to.class.Name module"
        // The arrow (->) separates source from target
        var parts = line.Split([" -> ", " ", "\t"],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return (null, null)!;
        }

        var fromPart = parts[0];
        var toPart = parts[1];
        // Skip the rest.

        return (fromPart, toPart);
    }

    private static bool CanParseLine(string line)
    {
        return !string.IsNullOrWhiteSpace(line) && !line.Contains("not found") && !line.StartsWith("classes");
    }

    private CodeElement GetOrCreateCodeElementHierarchy(string fullClassName)
    {
        if (string.IsNullOrWhiteSpace(fullClassName))
        {
            throw new ArgumentException(nameof(fullClassName));
        }

        if (_codeElements.TryGetValue(fullClassName, out var existingElement))
        {
            return existingElement;
        }

        var parts = fullClassName.Split('.');
        var currentPath = "";
        CodeElement? leafElement = null;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var previousPath = currentPath;
            currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "." + part;

            if (!_codeElements.ContainsKey(currentPath))
            {
                var elementType = GetCodeElementType(i, parts);

                var parent = string.IsNullOrEmpty(previousPath) ? null : _codeElements[previousPath];
                var codeElement = new CodeElement($"jdeps_{_nextId++}", elementType, part, currentPath, parent);
                parent?.Children.Add(codeElement);
                _codeElements[currentPath] = codeElement;
                leafElement = codeElement;
            }
        }

        if (leafElement is null || leafElement.ElementType != CodeElementType.Class)
        {
            throw new InvalidOperationException("Parser error");
        }

        return leafElement;
    }

    private static CodeElementType GetCodeElementType(int i, string[] parts)
    {
        // Last part is always a class
        // Everything else is treated as a namespace/package
        var elementType = i == parts.Length - 1 ? CodeElementType.Class : CodeElementType.Namespace;
        return elementType;
    }
}