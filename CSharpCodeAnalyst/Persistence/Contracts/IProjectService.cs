namespace CSharpCodeAnalyst.Persistence.Contracts;

/// <summary>
///     Application-level project service.
///     Orchestrates the full project lifecycle: load/save, dirty-state tracking,
///     MRU management, and snapshot handling.
///     Keeps ViewModels free of direct persistence dependencies.
/// </summary>
/// <remarks>
///     This interface is defined in Phase 1 as a stable contract.
///     The concrete implementation replaces the persistence logic currently
///     embedded in MainViewModel and will be wired up in a later refactoring phase.
/// </remarks>
public interface IProjectService
{
    /// <summary>Raised after a project has been successfully loaded.</summary>
    event EventHandler<ProjectLoadedEventArgs>? ProjectLoaded;

    /// <summary>Raised after a project has been successfully saved. Arg is the file path.</summary>
    event EventHandler<string>? ProjectSaved;

    /// <summary>Raised whenever the dirty state changes.</summary>
    event EventHandler? DirtyStateChanged;

    bool IsDirty { get; }
    bool HasSnapshot { get; }

    /// <summary>
    ///     Path of the currently open project file, or <c>null</c> when no file has been saved yet.
    /// </summary>
    string? CurrentFilePath { get; }

    /// <summary>
    ///     Loads a project. When <paramref name="filePath" /> is <c>null</c> an open-file dialog
    ///     is shown to the user.
    /// </summary>
    Task LoadAsync(string? filePath = null);

    /// <summary>
    ///     Saves the current project. When <paramref name="forceNewFile" /> is <c>true</c>
    ///     a save-file dialog is always shown regardless of whether a current path exists.
    /// </summary>
    Task SaveAsync(bool forceNewFile = false);

    /// <summary>Creates an in-memory snapshot of the current project state.</summary>
    void CreateSnapshot();

    /// <summary>Restores the project to the last created snapshot.</summary>
    void RestoreSnapshot();

    /// <summary>Marks the project as modified.</summary>
    void MarkDirty(bool forceNewFile = false);

    /// <summary>Clears the dirty flag and records the saved file path.</summary>
    void ClearDirty(string filePath);
}
