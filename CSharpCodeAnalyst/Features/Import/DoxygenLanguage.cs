namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Source languages the doxygen based import supports. The language only decides which
///     files doxygen parses (and which directories are excluded) - the XML output and the
///     conversion to a code graph are identical: doxygen normalizes Python packages/modules
///     to namespace compounds with "::" separated names, just like C++ namespaces.
/// </summary>
public enum DoxygenLanguage
{
    Cpp,
    Python
}
