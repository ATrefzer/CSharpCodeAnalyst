namespace CSharpCodeAnalyst.Shared.Messages;

/// <summary>
///     Image export formats the web graph can produce.
/// </summary>
public enum WebGraphExportFormat
{
    Png,
    Svg,

    // Copy a PNG of the graph to the clipboard instead of saving to a file.
    ClipboardPng
}

/// <summary>
///     Asks the web graph adapter to export the current canvas as an image and save it.
///     The export is produced in JavaScript (e.g. cy.png) and saved by the adapter, so it
///     travels over the bus like the other render-only requests (Layout / Refit).
/// </summary>
public sealed record ExportWebGraphRequest(WebGraphExportFormat Format);
