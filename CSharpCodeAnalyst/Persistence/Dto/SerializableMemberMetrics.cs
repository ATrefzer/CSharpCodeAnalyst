namespace CSharpCodeAnalyst.Persistence.Dto;

[Serializable]
public class SerializableMemberMetrics(string elementId, int codeLines, int commentLines, int logicalLinesOfCode, int cyclomaticComplexity)
{
    public string ElementId { get; set; } = elementId;
    public int CodeLines { get; set; } = codeLines;
    public int CommentLines { get; set; } = commentLines;
    public int LogicalLinesOfCode { get; set; } = logicalLinesOfCode;
    public int CyclomaticComplexity { get; set; } = cyclomaticComplexity;
}
