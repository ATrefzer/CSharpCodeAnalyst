using System.IO;
using System.Reflection;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Importers.Contracts;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Host-side implementation of <see cref="IImportContext" />. Owns the scratch directory for the
///     duration of one import and deletes it afterwards.
/// </summary>
internal sealed class ImportContext : IImportContext, IDisposable
{
    private readonly Lazy<string> _workingDirectory;

    public ImportContext(IUserNotification userNotification, IProgress<string> progress, IImporter importer,
        CancellationToken cancellationToken = default)
    {
        UserNotification = userNotification;
        Progress = progress;
        CancellationToken = cancellationToken;

        // The directory of the assembly the importer came from, so an importer that ships assets
        // keeps working when it is no longer next to the executable.
        AssetDirectory = Path.GetDirectoryName(importer.GetType().Assembly.Location) ?? AppContext.BaseDirectory;

        // Created on first use: most importers never need it.
        _workingDirectory = new Lazy<string>(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), "CSharpCodeAnalyst", "import", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        });
    }

    public void Dispose()
    {
        if (!_workingDirectory.IsValueCreated)
        {
            return;
        }

        try
        {
            Directory.Delete(_workingDirectory.Value, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort - the directory lives below %TEMP% anyway.
        }
    }

    public IUserNotification UserNotification { get; }
    public IProgress<string> Progress { get; }
    public string AssetDirectory { get; }
    public CancellationToken CancellationToken { get; }

    public string WorkingDirectory
    {
        get => _workingDirectory.Value;
    }
}
