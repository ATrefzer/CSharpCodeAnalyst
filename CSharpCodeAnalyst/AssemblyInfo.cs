using System.Windows;
using System.Runtime.CompilerServices;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
    //(used if a resource is not found in the page,
    // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
    //(used if a resource is not found in the page,
    // app, or any theme specific resource dictionaries)
)]

// Allow test project to access internal types for unit testing (e.g., MsaglHierarchicalBuilder)
[assembly: InternalsVisibleTo("Tests")]