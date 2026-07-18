using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Persistence.Dto;

[Serializable]
public class SerializableCodeElement(
    string id,
    string name,
    string fullName,
    CodeElementType elementType,
    List<SourceLocation> sourceLocations,
    HashSet<string> attributes,
    bool isExternal = false)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string FullName { get; set; } = fullName;
    public CodeElementType ElementType { get; set; } = elementType;
    public List<SourceLocation> SourceLocations { get; set; } = sourceLocations;
    public HashSet<string> Attributes { get; set; } = attributes;

    /// <summary>
    ///     Whether the element belongs to a referenced assembly rather than the parsed solution.
    /// </summary>
    public bool IsExternal { get; set; } = isExternal;
}
