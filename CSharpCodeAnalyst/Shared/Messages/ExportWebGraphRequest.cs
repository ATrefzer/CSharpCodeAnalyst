namespace CSharpCodeAnalyst.Shared.Messages;

/// <summary>
///     Image export formats the web graph can produce.
/// </summary>
public enum WebGraphExportFormat
{
    Png,

    // SVG needs the cytoscape-svg extension (not bundled yet) — reserved for later.
    Svg
}

/// <summary>
///     Asks the web graph adapter to export the current canvas as an image and save it.
///     The export is produced in JavaScript (e.g. cy.png) and saved by the adapter, so it
///     travels over the bus like the other render-only requests (Layout / Refit).
/// </summary>
public sealed record ExportWebGraphRequest(WebGraphExportFormat Format);
