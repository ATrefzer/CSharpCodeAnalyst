namespace CSharpCodeAnalyst.Shared.Messages;

/// <summary>
///     Asks the graph render adapters to recompute the layout from scratch (re-run the
///     layout algorithm and reposition all nodes). This is a render-only operation — the
///     shared model does not change — so it travels over the bus rather than via Changed.
/// </summary>
public sealed record RelayoutGraphRequest;
