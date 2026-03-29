using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSharpCodeAnalyst.Configuration;

/// <summary>
///     Manages user-specific persistent settings (userSettings.json).
/// </summary>
public class UserSettings
{
    private string _settingsPath;

    [JsonConstructor]
    private UserSettings()
    {
        _settingsPath = string.Empty;
    }

    private UserSettings(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public const string DefaultAiEndpoint = "https://api.anthropic.com/v1/messages";
    public const string DefaultAiModel = "claude-opus-4-6";

    public List<string> RecentFiles { get; set; } = [];

    public string AiEndpoint { get; set; } = DefaultAiEndpoint;

    public string AiModel { get; set; } = DefaultAiModel;

    public static UserSettings Instance { get; } = LoadOrCreate();

    private static UserSettings LoadOrCreate()
    {
        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CSharpCodeAnalyst");
        Directory.CreateDirectory(appDir);

        var settingsPath = Path.Combine(appDir, "userSettings.json");

        if (File.Exists(settingsPath))
        {
            try
            {
                var json = File.ReadAllText(settingsPath);
                var loaded = JsonSerializer.Deserialize<UserSettings>(json);
                if (loaded != null)
                {
                    loaded._settingsPath = settingsPath;
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                // No settings file
                Trace.TraceError(ex.ToString());
            }
        }

        return new UserSettings(settingsPath);
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    public void AddRecentFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        RecentFiles.Remove(filePath);
        RecentFiles.Insert(0, filePath);
        if (RecentFiles.Count > 10)
        {
            RecentFiles.RemoveAt(10);
        }

        Save();
    }

    public UserSettings Clone()
    {
        return new UserSettings
        {
            RecentFiles = new List<string>(this.RecentFiles),
            AiEndpoint = this.AiEndpoint,
            AiModel = this.AiModel,
            _settingsPath = this._settingsPath
        };
    }
}