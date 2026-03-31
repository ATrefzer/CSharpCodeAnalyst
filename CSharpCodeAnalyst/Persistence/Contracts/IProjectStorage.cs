using CSharpCodeAnalyst.Persistence.Dto;
using CSharpCodeAnalyst.Shared;

namespace CSharpCodeAnalyst.Persistence.Contracts;

/// <summary>
///     Low-level persistence contract: serializes and deserializes project data to/from a
///     storage medium. Implementations are storage-technology specific (e.g. JSON files).
///     Has no UI dependency — no file dialogs, no notifications, no events.
///     Callers are responsible for providing file paths and handling user feedback.
/// </summary>
public interface IProjectStorage
{
    bool HasSnapshot { get; }

    /// <summary>Loads project data from the given file path.</summary>
    Task<Result<ProjectData>> LoadFromFileAsync(string filePath);

    /// <summary>Saves project data to the given file path.</summary>
    Result SaveToFile(ProjectData data, string filePath);

    /// <summary>Stores an in-memory snapshot of the given project data.</summary>
    void CreateSnapshot(ProjectData data);

    /// <summary>
    ///     Returns the latest snapshot, or a failure result when none exists.
    /// </summary>
    Result<ProjectData> RestoreSnapshot();
}
