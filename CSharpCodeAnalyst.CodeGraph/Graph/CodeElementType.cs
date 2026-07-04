namespace CSharpCodeAnalyst.CodeGraph.Graph;

public enum CodeElementType
{
    Assembly,
    Namespace,
    Class,
    Interface,
    Struct,
    Method,
    Property,

    /// <summary>
    ///     A getter or setter of a property. Only created when the parser is configured to split
    ///     property accessors; otherwise a property is a single <see cref="Property" /> element.
    /// </summary>
    PropertyAccessor,
    Delegate,
    Event,
    Enum,
    Field,
    Record,
    Other
}