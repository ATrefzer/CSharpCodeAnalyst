namespace CSharpCodeAnalyst.CodeGraph.Graph;

public static class CodeElementExtensions
{
    /// <summary>
    ///     Whether the element is a type declaration (class, interface, struct, record, enum or
    ///     delegate) as opposed to a member (method, field, ...) or a container (namespace,
    ///     assembly). Shared definition so every analysis and UI feature agrees on what "a type" is.
    /// </summary>
    public static bool IsType(this CodeElement element)
    {
        return element.ElementType.IsType();
    }

    /// <inheritdoc cref="IsType(CodeElement)" />
    public static bool IsType(this CodeElementType elementType)
    {
        return elementType is CodeElementType.Class or CodeElementType.Interface
            or CodeElementType.Struct or CodeElementType.Record or CodeElementType.Enum
            or CodeElementType.Delegate;
    }
}
