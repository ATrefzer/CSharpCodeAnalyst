using System.Windows.Input;

namespace CSharpCodeAnalyst.Areas.TableArea;

/// <summary>
///     Definition für eine Spalte
/// </summary>
public interface ITableColumnDefinition
{
    /// <summary>
    ///     Name der Property im Datenobjekt (für Binding)
    /// </summary>
    string PropertyName { get; }

    /// <summary>
    ///     Anzeigename für Spalten-Header
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Typ der Spalte (bestimmt das Rendering)
    /// </summary>
    ColumnType Type { get; }

    /// <summary>
    ///     Breite der Spalte (0 = Auto)
    /// </summary>
    double Width { get; }

    /// <summary>
    ///     Command für Click-Events (z.B. bei Links)
    /// </summary>
    ICommand? ClickCommand { get; }

    /// <summary>
    ///     Parameter für das Command
    /// </summary>
    object? CommandParameter { get; }

    bool IsExpandable { get; set; }
}