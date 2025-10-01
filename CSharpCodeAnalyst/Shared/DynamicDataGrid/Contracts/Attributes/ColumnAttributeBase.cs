namespace CSharpCodeAnalyst.Areas.DynamicDataGrid.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public abstract class ColumnAttributeBase : Attribute
{
    public string Header { get; set; } = string.Empty;
}