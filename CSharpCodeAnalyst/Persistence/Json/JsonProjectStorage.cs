using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpCodeAnalyst.Features.Import;
using CSharpCodeAnalyst.Persistence.Contracts;
using CSharpCodeAnalyst.Persistence.Dto;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared;
using CSharpCodeAnalyst.Shared.Notifications;

namespace CSharpCodeAnalyst.Persistence.Json;

/// <summary>
///     JSON-file based implementation of <see cref="IProjectStorage" />.
/// </summary>
public class JsonProjectStorage : IProjectStorage
{
    private readonly IUserNotification _ui;
    private ProjectData? _snapshot;

    public JsonProjectStorage(IUserNotification ui)
    {
        _ui = ui;
    }

    public bool HasSnapshot => _snapshot != null;

    public event EventHandler<ImportStateChangedArgs>? LoadingStateChanged;

    /// <inheritdoc />
    public async Task<Result<(string fileName, ProjectData data)>> LoadAsync()
    {
        var fileName = _ui.ShowOpenFileDialog("JSON files (*.json)|*.json", "Load Project");
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<(string, ProjectData)>.Canceled();
        }

        return await LoadFromFileAsync(fileName);
    }

    /// <inheritdoc />
    public async Task<Result<(string fileName, ProjectData data)>> LoadFromFileAsync(string filePath)
    {
        try
        {
            OnLoadingStateChanged("Loading...", true);

            var projectData = await Task.Run(() => DeserializeProject(filePath));
            return Result<(string, ProjectData)>.Success((filePath, projectData));
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Result<(string, ProjectData)>.Failure(ex);
        }
        finally
        {
            OnLoadingStateChanged(string.Empty, false);
        }
    }

    /// <inheritdoc />
    public Result<string> Save(ProjectData data, string? currentFilePath = null)
    {
        if (!TryGetSaveFilePath(currentFilePath, out var filePath))
        {
            return Result<string>.Canceled();
        }

        try
        {
            SerializeProject(data, filePath);
            return Result<string>.Success(filePath);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Result<string>.Failure(ex);
        }
    }

    /// <inheritdoc />
    public void CreateSnapshot(ProjectData data)
    {
        _snapshot = data;
        _ui.ShowSuccess(Strings.Snapshot_Success);
    }

    /// <inheritdoc />
    public void RestoreSnapshot(Action<ProjectData> restoreAction)
    {
        if (!HasSnapshot)
        {
            _ui.ShowWarning(Strings.Restore_NoSnapshot);
            return;
        }

        try
        {
            OnLoadingStateChanged(Strings.Restore_LoadMessage, true);
            restoreAction(_snapshot!);
            _ui.ShowSuccess(Strings.Restore_Success);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            OnLoadingStateChanged(string.Empty, false);
        }
    }

    private void OnLoadingStateChanged(string message, bool isLoading)
    {
        LoadingStateChanged?.Invoke(this, new ImportStateChangedArgs(message, isLoading));
    }

    private void ShowError(Exception ex)
    {
        var message = string.Format(Strings.OperationFailed_Message, ex.Message);
        _ui.ShowError(message);
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
        var options = new JsonSerializerOptions
        {
            WriteIndented = false
        };
        var json = JsonSerializer.Serialize(projectData, options);
        File.WriteAllText(filePath, json);
    }

    private bool TryGetSaveFilePath(string? currentFilePath, out string outFileName)
    {
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            outFileName = currentFilePath;
            return true;
        }

        var fileName = _ui.ShowSaveFileDialog("JSON files (*.json)|*.json", "Save Project");
        if (string.IsNullOrEmpty(fileName))
        {
            outFileName = string.Empty;
            return false;
        }

        outFileName = fileName;
        return true;
    }
}
