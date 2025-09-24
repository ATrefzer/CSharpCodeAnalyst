namespace CSharpCodeAnalyst.Areas.TableArea
{
    /// <summary>
    ///     Available column types for the dynamic data grid
    /// </summary>
    public enum ColumnType
    {
        Text,
        Link,
        Image,
        Toggle,

        /// <summary>
        ///     Custom Template (requires ICustomColumnDefinition)
        /// </summary>
        Custom
    }
}