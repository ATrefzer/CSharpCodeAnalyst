using System.IO;
using System.Text.Json;

namespace CSharpCodeAnalyst.Configuration;

public class ApplicationSettings
{
    private string _defaultProjectExcludeFilter = string.Empty;
    public int WarningCodeElementLimit { get; set; } = 300;
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
    

    public string DefaultProjectExcludeFilter
    {
        get => CleanupProjectFilters(_defaultProjectExcludeFilter);
        set => _defaultProjectExcludeFilter = CleanupProjectFilters(value);
    }

    public bool AutomaticallyAddContainingType { get; set; } = true;

    public bool IncludeExternalCode { get; set; } = false;

    public bool WarnIfFiltersActive { get; set; } = true;

    public void Save(string appSettingsPath)
    {
        var root = new { ApplicationSettings = this };
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(root, options);
        File.WriteAllText(appSettingsPath, json);
    }
}