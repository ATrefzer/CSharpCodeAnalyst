namespace CSharpCodeAnalyst.Shared.Messages;

/// <summary>
///     Asks the graph view model to remove the currently selected elements (with their
///     children) from the working graph. Raised from the Delete shortcut in the web graph,
///     which — like the explore shortcuts — runs over JS so it also fires when the canvas
///     has keyboard focus.
/// </summary>
public sealed record RemoveSelectedElementsRequest;
