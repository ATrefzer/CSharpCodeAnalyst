using System.Windows.Input;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     One entry of the import menu.
///     The menu is a single bound list rather than hardcoded items, so registering an importer needs
///     no XAML change. It has to be uniform because an ItemsControl cannot mix ItemsSource with
///     explicit children - and the C# solution import, which is not an <see cref="Contracts" />
///     importer, still belongs in that menu. Hence each entry carries its own command instead of the
///     menu special-casing one of them.
/// </summary>
public sealed record ImportMenuEntry(string Label, string Description, ICommand Command, object? CommandParameter);
