using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpCodeAnalyst.Persistence.Contracts;
using CSharpCodeAnalyst.Persistence.Dto;
using CSharpCodeAnalyst.Shared;

namespace CSharpCodeAnalyst.Persistence.Json;

/// <summary>
///     JSON-file based implementation of <see cref="IProjectStorage" />.
///     Pure I/O — no UI dependency, no file dialogs, no notifications, no events.
/// </summary>
public class JsonProjectStorage : IProjectStorage
{
    private ProjectData? _snapshot;

    public bool HasSnapshot => _snapshot != null;

    /// <inheritdoc />
    public async Task<Result<ProjectData>> LoadFromFileAsync(string filePath)
    {
        try
        {
            var projectData = await Task.Run(() => DeserializeProject(filePath));
            return Result<ProjectData>.Success(projectData);
        }
        catch (Exception ex)
        {
            return Result<ProjectData>.Failure(ex);
        }
    }

    /// <inheritdoc />
    public Result SaveToFile(ProjectData data, string filePath)
    {
        try
        {
            SerializeProject(data, filePath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex);
        }
    }

    /// <inheritdoc />
    public void CreateSnapshot(ProjectData data)
    {
        _snapshot = data;
    }

    /// <inheritdoc />
    public Result<ProjectData> RestoreSnapshot()
    {
        if (_snapshot is null)
        {
            return Result<ProjectData>.Failure(new InvalidOperationException("No snapshot available."));
        }

        return Result<ProjectData>.Success(_snapshot);
    }

    private static ProjectData DeserializeProject(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve
        };
        return JsonSerializer.Deserialize<ProjectData>(json, options)
               ?? throw new InvalidOperationException("Failed to deserialize project");
    }

    private static void SerializeProject(ProjectData projectData, string filePath)
    {
        var options = new JsonSerializerOptions { WriteIndented = false };
        var json = JsonSerializer.Serialize(projectData, options);
        File.WriteAllText(filePath, json);
    }
}