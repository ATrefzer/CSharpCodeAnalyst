namespace CSharpCodeAnalyst.History.Model;

/// <summary>
///     Chaining of multiple filters. All filters must accept!
/// </summary>
public sealed class Filter : IFilter
{
    private readonly IFilter[] _filters;

    public Filter(params IFilter[] filters)
    {
        var nonNull = filters.Where(filter => filter != null);
        _filters = nonNull.ToArray();
    }

    public bool IsAccepted(string path)
    {
        if (!_filters.Any())
        {
            return true;
        }

        return _filters.All(filter => filter.IsAccepted(path));
    }
}

/// <summary>
///     Only files from a given list is accepted.
/// </summary>
public class FileFilter : IFilter
{
    private readonly HashSet<string> _acceptedFiles;

    public FileFilter(IEnumerable<string> acceptedFiles)
    {
        _acceptedFiles = new HashSet<string>(acceptedFiles.Select(x => x.ToLowerInvariant()));
    }

    public bool IsAccepted(string path)
    {
        var accepted = _acceptedFiles.Contains(path.ToLowerInvariant());
        return accepted;
    }
}

/// <summary>
///     Given extensions are allowed!
/// </summary>
public sealed class ExtensionIncludeFilter : IFilter
{
    private readonly string[] _allowedExtensions;

    public ExtensionIncludeFilter(params string[] allowedExtensions)
    {
        _allowedExtensions = allowedExtensions.Select(x => x.ToLowerInvariant()).ToArray();
    }

    public bool IsAccepted(string path)
    {
        var accepted = _allowedExtensions.Any(path.ToLowerInvariant().EndsWith);
        return accepted;
    }
}