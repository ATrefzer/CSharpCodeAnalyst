using CSharpCodeAnalyst.Importers.Contracts;
using CSharpCodeAnalyst.Importers.Dart;
using CSharpCodeAnalyst.Importers.Doxygen;
using CSharpCodeAnalyst.Importers.Jdeps;
using CSharpCodeAnalyst.Importers.PlainText;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     The registry of importers, mirroring <see cref="Analyzers.AnalyzerManager" />. The import
///     menu binds to <see cref="All" />, so adding an importer means adding one line here - no XAML
///     change.
///     The C# solution import is deliberately not in this list: it takes its options from the
///     settings rather than from a dialog and is still driven by <see cref="Importer" />. The
///     contract would fit it, and it should move here once that path is reworked.
/// </summary>
internal sealed class ImporterManager
{
    private readonly Dictionary<string, IImporter> _importers = [];

    public ImporterManager()
    {
        // Order defines the order in the menu.
        Add(new DoxygenImporter());
        Add(new DartImporter());
        Add(new JdepsImporter());
        Add(new PlainTextImporter());
    }

    public IEnumerable<IImporter> All
    {
        get => _importers.Values.ToList();
    }

    public IImporter Get(string id)
    {
        return _importers[id];
    }

    private void Add(IImporter importer)
    {
        _importers.Add(importer.Id, importer);
    }
}
