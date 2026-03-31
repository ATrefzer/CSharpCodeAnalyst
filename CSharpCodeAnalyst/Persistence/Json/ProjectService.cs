using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Features.Import;
using CSharpCodeAnalyst.Persistence.Contracts;
using CSharpCodeAnalyst.Persistence.Dto;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Notifications;

namespace CSharpCodeAnalyst.Persistence.Json;

/// <summary>
///     Default implementation of <see cref="IProjectService" />.
///     Orchestrates <see cref="IProjectStorage" /> with file-dialog handling, user notifications,
///     dirty-state tracking, and MRU management.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly IProjectStorage _storage;
    private readonly IUserNotification _ui;
    private readonly UserPreferences _userPreferences;

    private DirtyState _dirtyState = DirtyState.Saved;

    public ProjectService(IProjectStorage storage, IUserNotification ui, UserPreferences userPreferences)
    {
        _storage = storage;
        _ui = ui;
        _userPreferences = userPreferences;
    }

    public event EventHandler<ProjectLoadedEventArgs>? ProjectLoaded;
    public event EventHandler<string>? ProjectSaved;
    public event EventHandler? DirtyStateChanged;
    public event EventHandler<ImportStateChangedArgs>? ProgressChanged;

    public bool IsDirty => _dirtyState != DirtyState.Saved;
    public bool RequiresNewFilePath => _dirtyState == DirtyState.DirtyForceNewFile;
    public bool HasSnapshot => _storage.HasSnapshot;
    public string? CurrentFilePath { get; private set; }

    /// <inheritdoc />
    public async Task LoadAsync(string? filePath = null)
    {
        var path = filePath ?? _ui.ShowOpenFileDialog("JSON files (*.json)|*.json", "Load Project");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        ProgressChanged?.Invoke(this, new ImportStateChangedArgs("Loading...", true));
        try
        {
            var result = await _storage.LoadFromFileAsync(path);

            if (result.IsSuccess)
            {
                ClearDirty(path);
                _userPreferences.AddRecentFile(path);
                ProjectLoaded?.Invoke(this, new ProjectLoadedEventArgs(result.Data!, path));
            }
            else
            {
                _ui.ShowError(string.Format(Strings.OperationFailed_Message, result.Error!.Message));
            }
        }
        finally
        {
            ProgressChanged?.Invoke(this, new ImportStateChangedArgs(string.Empty, false));
        }
    }

    /// <inheritdoc />
    public void Save(ProjectData data, bool forceNewFile = false)
    {
        var path = ResolveSavePath(forceNewFile);
        if (path is null)
        {
            return;
        }

        var result = _storage.SaveToFile(data, path);

        if (result.IsSuccess)
        {
            ClearDirty(path);
            _userPreferences.AddRecentFile(path);
            ProjectSaved?.Invoke(this, path);
        }
        else
        {
            _ui.ShowError(string.Format(Strings.OperationFailed_Message, result.Error!.Message));
        }
    }

    /// <inheritdoc />
    public void CreateSnapshot(ProjectData data)
    {
        _storage.CreateSnapshot(data);
        _ui.ShowSuccess(Strings.Snapshot_Success);
    }

    /// <inheritdoc />
    public void RestoreSnapshot()
    {
        if (!HasSnapshot)
        {
            _ui.ShowWarning(Strings.Restore_NoSnapshot);
            return;
        }

        var result = _storage.RestoreSnapshot();

        if (result.IsSuccess)
        {
            // Restoring does not change the current file path.
            ProjectLoaded?.Invoke(this, new ProjectLoadedEventArgs(result.Data!, CurrentFilePath ?? string.Empty));
            _ui.ShowSuccess(Strings.Restore_Success);
        }
        else
        {
            _ui.ShowError(string.Format(Strings.OperationFailed_Message, result.Error!.Message));
        }
    }

    /// <inheritdoc />
    public void MarkDirty(bool forceNewFile = false)
    {
        if (_dirtyState == DirtyState.DirtyForceNewFile)
        {
            return;
        }

        _dirtyState = forceNewFile ? DirtyState.DirtyForceNewFile : DirtyState.Dirty;

        if (forceNewFile)
        {
            CurrentFilePath = null;
        }

        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void StartNewProject()
    {
        CurrentFilePath = null;
        _dirtyState = DirtyState.Dirty;
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearDirty(string filePath)
    {
        CurrentFilePath = filePath;
        _dirtyState = DirtyState.Saved;
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? ResolveSavePath(bool forceNewFile)
    {
        if (!forceNewFile && !string.IsNullOrEmpty(CurrentFilePath))
        {
            return CurrentFilePath;
        }

        var path = _ui.ShowSaveFileDialog("JSON files (*.json)|*.json", "Save Project");
        return string.IsNullOrEmpty(path) ? null : path;
    }

    private enum DirtyState
    {
        Saved,
        Dirty,
        DirtyForceNewFile
    }
}