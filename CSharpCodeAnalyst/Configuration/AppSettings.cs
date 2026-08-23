using System.IO;
using System.Text.Json;

namespace CSharpCodeAnalyst.Configuration;

public class AppSettings
{
    public int WarningCodeElementLimit { get; set; } = 300;

    public string DefaultProjectExcludeFilter
    {
        get => CleanupProjectFilters(field);
        set => field = CleanupProjectFilters(value);
    } = string.Empty;

    /// <summary>
    ///     When elements are added to the canvas (exploring, dragging from the tree, ...), fill in any
    ///     containers missing between them and what is already shown, so a method does not show up
    ///     disconnected below a namespace several levels above it. Unlike the "complete to containing
    ///     types" toolbar command this never reaches out to add a containing type nobody asked for - it
    ///     only fills gaps between elements that are already known.
    /// </summary>
    public bool AutomaticallyFillGapsInHierarchy { get; set; } = true;

    public bool IncludeExternalCode { get; set; }

    public bool WarnIfFiltersActive { get; set; } = true;

    /// <summary>
    ///     When a solution is imported, fill the canvas with the whole graph collapsed to give an
    ///     immediate overview instead of an empty canvas.
    /// </summary>
    public bool ShowOverviewOnImport { get; set; } = true;

    /// <summary>
    ///     TCP port for the MCP endpoint, bound to loopback only. Configurable because the default may
    ///     already be taken - the client configuration has to name the same port.
    /// </summary>
    public int McpServerPort { get; set; } = 5178;

    public static string CleanupProjectFilters(string filterText)
    {
        char[] separators = [';', '\n', '\r'];
        var parts = filterText
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();
        return string.Join(";", parts);
    }

    public void Save(string appSettingsPath)
    {
        // Keep "ApplicationSettings" as the JSON section key for backward compatibility
        // with existing appsettings.json files.
        var root = new { ApplicationSettings = this };
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(root, options);
        File.WriteAllText(appSettingsPath, json);
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            WarningCodeElementLimit = this.WarningCodeElementLimit,
            DefaultProjectExcludeFilter = this.DefaultProjectExcludeFilter,
            AutomaticallyFillGapsInHierarchy = this.AutomaticallyFillGapsInHierarchy,
            IncludeExternalCode = this.IncludeExternalCode,
            WarnIfFiltersActive = this.WarnIfFiltersActive,
            ShowOverviewOnImport = this.ShowOverviewOnImport,
            McpServerPort = this.McpServerPort
        };
    }
}
