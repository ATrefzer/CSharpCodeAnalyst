using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Features.Import;
using CSharpCodeAnalyst.Persistence.Contracts;
using CSharpCodeAnalyst.Persistence.Dto;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Notifications;

namespace CSharpCodeAnalyst.Persistence.Json;

/// <summary>
///     Default implementation of <see cref="IProjectService" />.
///     Orchestrates <see cref="IProjectStorage" /> with dirty-state tracking and MRU management.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly IProjectStorage _storage;
    private readonly IUserNotification _ui;
    private readonly UserSettings _userSettings;

    private DirtyState _dirtyState = DirtyState.Saved;
    private string? _currentFilePath;

    public ProjectService(IProjectStorage storage, IUserNotification ui, UserSettings userSettings)
    {
        _storage = storage;
        _ui = ui;
        _userSettings = userSettings;
        _storage.LoadingStateChanged += (s, e) => ProgressChanged?.Invoke(this, e);
    }

    public event EventHandler<ProjectLoadedEventArgs>? ProjectLoaded;
    public event EventHandler<string>? ProjectSaved;
    public event EventHandler? DirtyStateChanged;
    public event EventHandler<ImportStateChangedArgs>? ProgressChanged;

    public bool IsDirty => _dirtyState != DirtyState.Saved;
    public bool RequiresNewFilePath => _dirtyState == DirtyState.DirtyForceNewFile;
    public bool HasSnapshot => _storage.HasSnapshot;
    public string? CurrentFilePath => _currentFilePath;

    /// <inheritdoc />
    public async Task LoadAsync(string? filePath = null)
    {
        var result = filePath is null
            ? await _storage.LoadAsync()
            : await _storage.LoadFromFileAsync(filePath);

        if (result.IsSuccess)
        {
            var (fileName, data) = result.Data;
            ClearDirty(fileName);
            _userSettings.AddRecentFile(fileName);
            ProjectLoaded?.Invoke(this, new ProjectLoadedEventArgs(data, fileName));
        }
    }

    /// <inheritdoc />
    public void Save(ProjectData data, bool forceNewFile = false)
    {
        var path = forceNewFile ? null : _currentFilePath;
        var result = _storage.Save(data, path);

        if (result.IsSuccess)
        {
            ClearDirty(result.Data!);
            _userSettings.AddRecentFile(result.Data!);
            ProjectSaved?.Invoke(this, result.Data!);
        }
    }

    /// <inheritdoc />
    public void CreateSnapshot(ProjectData data)
    {
        _storage.CreateSnapshot(data);
    }

    /// <inheritdoc />
    public void RestoreSnapshot()
    {
        _storage.RestoreSnapshot(data =>
        {
            // Restoring does not change the current file path.
            ProjectLoaded?.Invoke(this, new ProjectLoadedEventArgs(data, _currentFilePath ?? string.Empty));
        });
    }

    /// <inheritdoc />
    public void MarkDirty(bool forceNewFile = false)
    {
        if (_dirtyState == DirtyState.DirtyForceNewFile)
        {
            // Most restrictive state wins – never downgrade.
            return;
        }

        _dirtyState = forceNewFile ? DirtyState.DirtyForceNewFile : DirtyState.Dirty;

        if (forceNewFile)
        {
            // The current file path is no longer valid after a structural model change.
            _currentFilePath = null;
        }

        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void StartNewProject()
    {
        _currentFilePath = null;
        _dirtyState = DirtyState.Dirty;
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearDirty(string filePath)
    {
        _currentFilePath = filePath;
        _dirtyState = DirtyState.Saved;
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private enum DirtyState
    {
        Saved,
        Dirty,

        /// <summary>
        ///     The model was structurally changed (e.g. refactoring). The previous file path
        ///     is invalid; a Save-As dialog must be shown on the next save.
        /// </summary>
        DirtyForceNewFile
    }
}
