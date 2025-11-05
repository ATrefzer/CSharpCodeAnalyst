using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Project;

[Serializable]
public class SerializableCodeElement(
    string id,
    string name,
    string fullName,
    CodeElementType elementType,
    List<SourceLocation> sourceLocations,
    HashSet<string> attributes)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string FullName { get; set; } = fullName;
    public CodeElementType ElementType { get; set; } = elementType;
    public List<SourceLocation> SourceLocations { get; set; } = sourceLocations;
    public HashSet<string> Attributes { get; set; } = attributes;
}