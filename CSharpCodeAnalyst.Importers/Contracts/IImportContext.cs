using CSharpCodeAnalyst.AnalyzerSdk.Notifications;

namespace CSharpCodeAnalyst.Importers.Contracts;

/// <summary>
///     What the host provides to a running import.
/// </summary>
public interface IImportContext
{
    /// <summary>
    ///     Dialogs and messages. Constructed on the UI thread by the host.
    /// </summary>
    IUserNotification UserNotification { get; }

    /// <summary>
    ///     Status text while the import runs. Marshalled to the UI thread by the host, so it can be
    ///     called from the background.
    /// </summary>
    IProgress<string> Progress { get; }

    /// <summary>
    ///     A scratch directory owned by the host, created on first use and deleted afterwards. An
    ///     importer that shells out to a tool writes its intermediate files here.
    /// </summary>
    string WorkingDirectory { get; }

    /// <summary>
    ///     Where this importer's own files live - the directory of the assembly it was loaded from,
    ///     not the application directory.
    ///     This distinction is the whole point: an importer that ships assets (the Dart import ships
    ///     the extractor as Dart sources) must not assume it sits next to the executable, or it
    ///     breaks the moment importers are loaded from a plugin folder.
    /// </summary>
    string AssetDirectory { get; }

    CancellationToken CancellationToken { get; }
}
