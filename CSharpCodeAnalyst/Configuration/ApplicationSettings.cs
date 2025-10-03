using System.IO;
using System.Text.Json;

namespace CSharpCodeAnalyst.Configuration;

public class ApplicationSettings
{
    public int WarningCodeElementLimit { get; set; } = 300;
    public string DefaultProjectExcludeFilter { get; set; } = string.Empty;
    public bool AutomaticallyAddContainingType { get; set; } = true;

    public bool IncludeExternalCode { get; set; } = false;

    public void Save(string appSettingsPath)
    {
        var root = new { ApplicationSettings = this };
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(root, options);
        File.WriteAllText(appSettingsPath, json);
    }
}