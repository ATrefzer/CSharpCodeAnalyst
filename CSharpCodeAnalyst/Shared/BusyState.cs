namespace CSharpCodeAnalyst.Shared;

/// <summary>
///     Busy/status-bar state reported by long-running operations (import, project load/save, git
///     history extraction). Producers get a single <see cref="IProgress{T}" /> sink injected instead
///     of declaring their own bespoke event, so MainViewModel has one place to update
///     IsLoading/LoadMessage instead of three.
/// </summary>
public readonly record struct BusyState(string Message, bool IsLoading);
