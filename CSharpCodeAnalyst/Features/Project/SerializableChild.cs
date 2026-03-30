namespace CSharpCodeAnalyst.Project;

[Serializable]
public class SerializableChild(string childId, string parentId)
{
    public string ParentId { get; set; } = parentId;
    public string ChildId { get; set; } = childId;
}