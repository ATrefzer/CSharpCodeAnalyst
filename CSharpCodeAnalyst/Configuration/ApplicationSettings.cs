using System.IO;
using System.Text.Json;

namespace CSharpCodeAnalyst.Configuration;

public class ApplicationSettings
{
    public int WarningCodeElementLimit { get; set; } = 50;
    public string DefaultProjectExcludeFilter { get; set; } = string.Empty;
    public int MaxDegreeOfParallelism { get; set; } = 8;
    public bool AutomaticallyAddContainingType { get; set; } = true;

    public void Save(string appSettingsPath)
    {
        var root = new { ApplicationSettings = this };
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(root, options);
        File.WriteAllText(appSettingsPath, json);
    }
}