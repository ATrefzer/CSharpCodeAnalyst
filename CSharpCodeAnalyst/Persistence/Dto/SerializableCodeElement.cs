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
    bool isExternal = false,
    AccessLevel accessLevel = AccessLevel.Unknown,
    bool isGenerated = false)
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

    /// <summary>
    ///     Whether a tool wrote the element. Defaults to false, so a project file written before this
    ///     existed keeps loading - its elements simply carry no marking until the next parse.
    /// </summary>
    public bool IsGenerated { get; set; } = isGenerated;

    /// <summary>
    ///     How far the element can be reached from. Defaults to Unknown, so a project file written before
    ///     this existed keeps loading - the elements simply carry no visibility until the next parse.
    /// </summary>
    public AccessLevel AccessLevel { get; set; } = accessLevel;
}
