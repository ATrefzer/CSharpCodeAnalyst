namespace CSharpCodeAnalyst.Shared.Messages;

/// <summary>
///     Asks the web graph adapter to abort an in-progress render. Because the layout runs
///     synchronously on the WebView2 render thread, a blocked run cannot be interrupted from
///     C# by messaging — the adapter terminates the render process and reloads the page.
///     This is a render-only operation; the shared model is not changed.
/// </summary>
public sealed record CancelWebRenderRequest;
