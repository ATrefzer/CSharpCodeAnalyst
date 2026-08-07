using System.IO;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Sarif;

/// <summary>
///     Turns the absolute Windows paths the parser produces into the URIs SARIF wants.
///     <para>
///         Everything below <see cref="SourceRootId" /> becomes a relative URI plus that base id, which
///         is what consumers like GitHub code scanning need to match a finding to a file in the
///         repository - an absolute "file:///D:/build-agent/..." path from a build agent matches
///         nothing. A file outside the root keeps an absolute file URI rather than being expressed
///         through a chain of "..", which SARIF consumers reject.
///     </para>
/// </summary>
internal sealed class SarifPathMapper
{
    public const string SourceRootId = "SRCROOT";

    private readonly string? _root;

    /// <param name="sourceRoot">Directory the relative URIs are built against. Null disables relativization.</param>
    public SarifPathMapper(string? sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            return;
        }

        try
        {
            // The trailing separator is what makes the Uri below a directory, and it is also what
            // keeps "C:\Src\App" from matching a sibling directory "C:\Src\AppTests".
            var full = Path.GetFullPath(sourceRoot);
            _root = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unusable root is not worth failing the whole validation run over - fall back to
            // absolute URIs.
            _root = null;
        }
    }

    /// <summary>The root as a directory URI, or <c>null</c> when there is none.</summary>
    public string? RootUri
    {
        get => _root is null ? null : new Uri(_root).AbsoluteUri;
    }

    public SarifArtifactLocation? Map(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(file);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (_root is not null && full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            var relative = full[_root.Length..];
            return new SarifArtifactLocation
            {
                Uri = EscapeRelativePath(relative),
                UriBaseId = SourceRootId
            };
        }

        return new SarifArtifactLocation { Uri = new Uri(full).AbsoluteUri };
    }

    /// <summary>
    ///     Escapes per segment, so that the separators stay separators. A path segment is escaped as a
    ///     URI data string because a file name may legitimately contain characters ('#', '?', spaces)
    ///     that would otherwise change the meaning of the URI.
    /// </summary>
    private static string EscapeRelativePath(string relative)
    {
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Join("/", segments.Select(Uri.EscapeDataString));
    }
}
