using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSharpCodeAnalyst.Configuration;

/// <summary>
///     Manages user-specific persistent preferences (userSettings.json in %LocalAppData%).
/// </summary>
public class UserPreferences
{
    private string _settingsPath;

    [JsonConstructor]
    private UserPreferences()
    {
        _settingsPath = string.Empty;
    }

    private UserPreferences(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public const string DefaultAiEndpoint = "https://api.anthropic.com/v1/messages";
    public const string DefaultAiModel = "claude-opus-4-6";

    public List<string> RecentFiles { get; set; } = [];

    public string AiEndpoint { get; set; } = DefaultAiEndpoint;

    public string AiModel { get; set; } = DefaultAiModel;

    /// <summary>
    ///     Loads user preferences from disk, or creates a new default instance when no file exists.
    ///     Call this once at startup and pass the result through dependency injection.
    /// </summary>
    public static UserPreferences LoadOrCreate()
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
                var loaded = JsonSerializer.Deserialize<UserPreferences>(json);
                if (loaded != null)
                {
                    loaded._settingsPath = settingsPath;
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        return new UserPreferences(settingsPath);
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

    public UserPreferences Clone()
    {
        return new UserPreferences
        {
            RecentFiles = new List<string>(this.RecentFiles),
            AiEndpoint = this.AiEndpoint,
            AiModel = this.AiModel,
            _settingsPath = this._settingsPath
        };
    }
}
