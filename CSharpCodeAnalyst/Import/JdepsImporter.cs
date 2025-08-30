using System.IO;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Import;

public class JdepsImporter
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


        // Create and return the CodeGraph
        var graph = new CodeGraph
        {
            Nodes = _codeElements.ToDictionary(c => c.Value.Id, c => c.Value)
        };

        return graph;
    }

    private void ParseLine(string line)
    {
        line = line.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.Contains("not found") || line.StartsWith("classes"))
        {
            return;
        }

        // Parse format: "from.class.Name -> to.class.Name module"
        // The arrow (->) separates source from target
        var parts = line.Split([" -> ", " ", "\t"],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return;
        }

        var fromPart = parts[0];
        var toPart = parts[1];
        // Skip the rest.

        // Create code elements for both from and to
        var from = CreateCodeElementHierarchy(fromPart);
        var to = CreateCodeElementHierarchy(toPart);

        // Add dependency
        var relationship = new Relationship(from.Id, to.Id, RelationshipType.Uses);
        from.Relationships.Add(relationship);
    }

    private CodeElement CreateCodeElementHierarchy(string fullClassName)
    {
        if (string.IsNullOrWhiteSpace(fullClassName))
        {
            throw new InvalidOperationException();
        }

        if (_codeElements.TryGetValue(fullClassName, out var result))
        {
            return result;
        }

        var parts = fullClassName.Split('.');
        var currentPath = "";

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var previousPath = currentPath;
            currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "." + part;

            if (!_codeElements.ContainsKey(currentPath))
            {
                CodeElementType elementType;
                if (i == parts.Length - 1)
                {
                    // Last part is always a class
                    elementType = CodeElementType.Class;
                }
                else
                {
                    // Everything else is treated as a namespace/package
                    elementType = CodeElementType.Namespace;
                }

                var parent = string.IsNullOrEmpty(previousPath) ? null : _codeElements[previousPath];
                var codeElement = new CodeElement($"jdeps_{_nextId++}", elementType, part, currentPath, parent);
                parent?.Children.Add(codeElement);
                _codeElements[currentPath] = codeElement;
                result = codeElement;
            }
        }

        if (result is null || result.ElementType != CodeElementType.Class)
        {
            throw new InvalidOperationException("Parser error");
        }

        return result;
    }
}