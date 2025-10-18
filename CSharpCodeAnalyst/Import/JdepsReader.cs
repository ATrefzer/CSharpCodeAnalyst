using System.IO;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Import;

public class JdepsReader
{
    private readonly Dictionary<string, CodeElement> _codeElements = new();
    private int _nextId = 1;

    public CodeGraph ImportFromFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        return ImportFromLines(lines);
    }

    public CodeGraph ImportFromLines(IEnumerable<string> lines)
    {
        _codeElements.Clear();
        _nextId = 1;

        // Parse all lines first
        foreach (var line in lines)
        {
            ParseLine(line.Trim());
        }


        var graph = new CodeGraph
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