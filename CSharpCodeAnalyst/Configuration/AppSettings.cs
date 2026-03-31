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

    public bool AutomaticallyAddContainingType { get; set; } = true;

    public bool IncludeExternalCode { get; set; }

    public bool WarnIfFiltersActive { get; set; } = true;

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
            AutomaticallyAddContainingType = this.AutomaticallyAddContainingType,
            IncludeExternalCode = this.IncludeExternalCode,
            WarnIfFiltersActive = this.WarnIfFiltersActive
        };
    }
}
