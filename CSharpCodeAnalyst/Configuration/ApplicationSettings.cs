using System.IO;
using System.Text.Json;

namespace CSharpCodeAnalyst.Configuration;

public class ApplicationSettings
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
        var root = new { ApplicationSettings = this };
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(root, options);
        File.WriteAllText(appSettingsPath, json);
    }

    public ApplicationSettings Clone()
    {
        return new ApplicationSettings
        {
            WarningCodeElementLimit = this.WarningCodeElementLimit,
            DefaultProjectExcludeFilter = this.DefaultProjectExcludeFilter,
            AutomaticallyAddContainingType = this.AutomaticallyAddContainingType,
            IncludeExternalCode = this.IncludeExternalCode,
            WarnIfFiltersActive = this.WarnIfFiltersActive
        };
    }
}