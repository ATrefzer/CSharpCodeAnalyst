using CSharpCodeAnalyst.Features.Import;
using CSharpCodeAnalyst.Persistence.Dto;

namespace CSharpCodeAnalyst.Persistence.Contracts;

/// <summary>
///     Application-level project service.
///     Orchestrates the full project lifecycle: load/save, dirty-state tracking,
///     MRU management, and snapshot handling.
///     Keeps ViewModels free of direct persistence dependencies.
/// </summary>
public interface IProjectService
{
    /// <summary>Raised after a project has been successfully loaded or a snapshot restored.</summary>
    event EventHandler<ProjectLoadedEventArgs>? ProjectLoaded;

    /// <summary>Raised after a project has been successfully saved. Arg is the file path.</summary>
    event EventHandler<string>? ProjectSaved;

    /// <summary>Raised whenever the dirty state changes.</summary>
    event EventHandler? DirtyStateChanged;

    /// <summary>Raised by the underlying storage when a long-running operation starts or ends.</summary>
    event EventHandler<ImportStateChangedArgs>? ProgressChanged;

    bool IsDirty { get; }

    /// <summary>
    ///     <c>true</c> when the model was structurally changed (e.g. after a refactoring) and
    ///     the previous file path is no longer valid. A Save-As dialog will always be shown.
    /// </summary>
    bool RequiresNewFilePath { get; }

    bool HasSnapshot { get; }

    /// <summary>Path of the currently open project file, or <c>null</c> when unsaved.</summary>
    string? CurrentFilePath { get; }

    /// <summary>
    ///     Loads a project. When <paramref name="filePath" /> is <c>null</c> an open-file dialog
    ///     is shown to the user.
    /// </summary>
    Task LoadAsync(string? filePath = null);

    /// <summary>
    ///     Saves the project data. When <paramref name="forceNewFile" /> is <c>true</c> a
    ///     save-file dialog is always shown regardless of whether a current path exists.
    /// </summary>
    void Save(ProjectData data, bool forceNewFile = false);

    /// <summary>Creates an in-memory snapshot of the given project data.</summary>
    void CreateSnapshot(ProjectData data);

    /// <summary>
    ///     Restores the project to the last created snapshot.
    ///     Raises <see cref="ProjectLoaded" /> if successful.
    /// </summary>
    void RestoreSnapshot();

    /// <summary>Marks the project as modified.</summary>
    void MarkDirty(bool forceNewFile = false);

    /// <summary>
    ///     Resets to an unsaved new project state (dirty, no file path).
    ///     Called after importing a solution or other data sources.
    /// </summary>
    void StartNewProject();
}
