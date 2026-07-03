namespace CSharpCodeAnalyst.Persistence.Dto;

[Serializable]
public class SerializableMemberMetrics(string elementId, int linesOfCode, int cyclomaticComplexity)
{
    public string ElementId { get; set; } = elementId;
    public int LinesOfCode { get; set; } = linesOfCode;
    public int CyclomaticComplexity { get; set; } = cyclomaticComplexity;
}
