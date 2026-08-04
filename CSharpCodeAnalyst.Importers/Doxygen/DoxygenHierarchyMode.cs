namespace CSharpCodeAnalyst.Importers.Doxygen;

/// <summary>
///     Where the namespace hierarchy of the imported graph comes from.
///     Only the parent of a top level element changes; nested types stay below their outer type
///     and members stay below their type in both modes.
/// </summary>
public enum DoxygenHierarchyMode
{
    /// <summary>
    ///     The scopes declared in the code: C++ namespaces, Java packages, Python packages/modules.
    ///     doxygen reports all of them as namespace compounds with "::" separated names.
    /// </summary>
    Declared,

    /// <summary>
    ///     The directory structure below the imported source directory. For code that is organized
    ///     by folders instead of namespaces - a common style in C++ - this is the structure the
    ///     author actually thinks in. The file name is not part of the hierarchy, so a header and
    ///     its implementation land in the same namespace.
    /// </summary>
    Directories
}
