namespace CSharpCodeAnalyst.MetricArea;

[AttributeUsage(AttributeTargets.Property)]
public abstract class ColumnAttributeBase : Attribute
{
    public string Header { get; set; } = string.Empty;
}