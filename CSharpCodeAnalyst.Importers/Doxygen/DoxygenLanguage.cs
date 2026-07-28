namespace CSharpCodeAnalyst.Importers.Doxygen;

/// <summary>
///     Source languages the doxygen based import supports. The language mainly decides which
///     files doxygen parses (and which directories are excluded) - the XML output and the
///     conversion to a code graph are nearly identical: doxygen normalizes Python packages/modules
///     and Java packages to namespace compounds with "::" separated names, just like C++ namespaces.
///     The one structural difference is Java: there an enum is a compound of its own
///     (see <see cref="DoxygenXmlConverter" />), while a C++ enum is a member of its scope.
/// </summary>
public enum DoxygenLanguage
{
    Cpp,
    Python,
    Java
}
