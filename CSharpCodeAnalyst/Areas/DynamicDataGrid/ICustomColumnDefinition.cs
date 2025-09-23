using System.Windows;

namespace CSharpCodeAnalyst.Areas.TableArea;

/// <summary>
///     Erweiterte Definition für Custom-Templates
/// </summary>
public interface ICustomColumnDefinition : IPluginColumnDefinition
{
    /// <summary>
    ///     Template für die Zellen-Darstellung
    /// </summary>
    DataTemplate CellTemplate { get; }

    /// <summary>
    ///     Optionales Template für Editing-Modus
    /// </summary>
    DataTemplate? EditingTemplate { get; }
}