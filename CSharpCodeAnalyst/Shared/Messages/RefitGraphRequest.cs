namespace CSharpCodeAnalyst.Shared.Messages;

/// <summary>
///     Asks the graph render adapters to recompute their size and fit the view, WITHOUT
///     re-running the layout (current node positions are kept). Render-only, like
///     <see cref="RelayoutGraphRequest" />.
/// </summary>
public sealed record RefitGraphRequest;
