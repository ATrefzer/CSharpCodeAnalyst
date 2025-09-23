using System.Windows;

namespace CSharpCodeAnalyst.Areas.TableArea;

/// <summary>
///     Haupt-Interface für Tabellendaten (ein Objekt für alles)
/// </summary>
public interface IPluginTableData
{
    /// <summary>
    ///     Gibt die Spaltendefinitionen zurück
    /// </summary>
    IEnumerable<IPluginColumnDefinition> GetColumns();

    /// <summary>
    ///     Gibt die Datenobjekte zurück
    /// </summary>
    IEnumerable<object> GetData();

    /// <summary>
    ///     Optionales Template für Row Details (kann null sein)
    /// </summary>
    DataTemplate? GetRowDetailsTemplate();

    /// <summary>
    ///     Optionaler Titel für die Tabelle (kann null sein)
    /// </summary>
    string? GetTitle();
}