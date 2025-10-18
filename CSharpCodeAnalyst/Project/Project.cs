using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Import;
using CSharpCodeAnalyst.Resources;
using Microsoft.Win32;

namespace CSharpCodeAnalyst.Project;

/// <summary>
///     Loading and saving project data.
/// </summary>
public class Project
{
    private ProjectData? _snapshot;

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
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "Load Project"
        };

        if (dialog.ShowDialog() != true)
        {
            return Result<(string, ProjectData)>.Canceled();
        }

        return await LoadProjectFromFileAsync(dialog.FileName);
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

    private static void ShowError(Exception ex)
    {
        var message = string.Format(Strings.OperationFailed_Message, ex.Message);
        MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
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

    public Result<bool> RestoreSnapshot(Action<ProjectData> restoreAction)
    {
        var result = Result<bool>.Success(true);
        try
        {
            OnLoadingStateChanged(Strings.Restore_LoadMessage, true);
            restoreAction(_snapshot); // Let MainViewModel handle it
        }
        catch (Exception ex)
        {
            result = Result<bool>.Failure(ex);
            ShowError(ex);
        }
        finally
        {
            OnLoadingStateChanged(string.Empty, false);
        }

        return result;
    }


    public void CreateSnapshot(ProjectData projectData)
    {
        _snapshot = projectData;
    }

    public ProjectData? GetSnapshot()
    {
        return _snapshot;
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

    private bool TryGetSaveFilePath(string? currentFilePath, out string filePath)
    {
        // If we have a current file path, reuse it
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            filePath = currentFilePath;
            return true;
        }

        // Otherwise show dialog
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "Save Project"
        };

        if (dialog.ShowDialog() == true)
        {
            filePath = dialog.FileName;
            return true;
        }

        filePath = string.Empty;
        return false;
    }
}