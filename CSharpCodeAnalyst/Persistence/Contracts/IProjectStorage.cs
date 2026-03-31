using CSharpCodeAnalyst.Features.Import;
using CSharpCodeAnalyst.Persistence.Dto;
using CSharpCodeAnalyst.Shared;

namespace CSharpCodeAnalyst.Persistence.Contracts;

/// <summary>
///     Low-level persistence contract: serializes and deserializes project data to/from a storage medium.
///     Implementations are storage-technology specific (e.g. JSON files, databases).
///     Does not own application state, dirty tracking, or MRU management.
/// </summary>
public interface IProjectStorage
{
    bool HasSnapshot { get; }

    event EventHandler<ImportStateChangedArgs>? LoadingStateChanged;

    /// <summary>
    ///     Shows an open-file dialog and loads the selected project.
    /// </summary>
    Task<Result<(string fileName, ProjectData data)>> LoadAsync();

    /// <summary>
    ///     Loads a project directly from the given file path without showing a dialog.
    /// </summary>
    Task<Result<(string fileName, ProjectData data)>> LoadFromFileAsync(string filePath);

    /// <summary>
    ///     Saves the project. If <paramref name="currentFilePath" /> is provided the file is
    ///     overwritten in place; otherwise a save-file dialog is shown.
    /// </summary>
    Result<string> Save(ProjectData data, string? currentFilePath = null);

    /// <summary>
    ///     Stores an in-memory snapshot of the given project data.
    /// </summary>
    void CreateSnapshot(ProjectData data);

    /// <summary>
    ///     Restores the latest snapshot by invoking <paramref name="restoreAction" /> with the
    ///     stored data. Shows a warning when no snapshot is available.
    /// </summary>
    void RestoreSnapshot(Action<ProjectData> restoreAction);
}
