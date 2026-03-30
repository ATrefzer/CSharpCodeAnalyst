using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Import;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Project;

/// <summary>
///     Loading and saving project data.
/// </summary>
public class Project
{
    private readonly IUserNotification _ui;
    private ProjectData? _snapshot;

    public Project(IUserNotification ui)
    {
        _ui = ui;
    }

    public bool HasSnapshot
    {
        get => _snapshot != null;
    }

    public event EventHandler<ImportStateChangedArgs>? LoadingStateChanged;


    /// <summary>
    ///     Shows file dialog and loads project from selected file.
    /// </summary>
    public async Task<Result<(string fileName, ProjectData)>> LoadProjectAsync()
    {
        var fileName = _ui.ShowOpenFileDialog("JSON files (*.json)|*.json", "Load Project");
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<(string, ProjectData)>.Canceled();
        }

        return await LoadProjectFromFileAsync(fileName);
    }

    /// <summary>
    ///     Loads project from specific file path.
    /// </summary>
    public async Task<Result<(string, ProjectData)>> LoadProjectFromFileAsync(string filePath)
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

    private void OnLoadingStateChanged(string message, bool isLoading)
    {
        LoadingStateChanged?.Invoke(this, new ImportStateChangedArgs(message, isLoading));
    }

    private void ShowError(Exception ex)
    {
        var message = string.Format(Strings.OperationFailed_Message, ex.Message);
        _ui.ShowError(message);
    }

    /// <summary>
    ///     Shows file dialog and saves project.
    /// </summary>
    public Result<string> SaveProject(ProjectData projectData, string? currentFilePath = null)
    {
        if (!TryGetSaveFilePath(currentFilePath, out var filePath))
        {
            return Result<string>.Canceled();
        }

        try
        {
            SerializeProject(projectData, filePath);
            return Result<string>.Success(filePath);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Result<string>.Failure(ex);
        }
    }

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
            restoreAction(_snapshot!); // Let MainViewModel handle it
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


    public void CreateSnapshot(ProjectData projectData)
    {
        _snapshot = projectData;
        _ui.ShowSuccess(Strings.Snapshot_Success);
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
        // If we have a current file path, reuse it
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