using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CSharpCodeAnalyst.History.Extensions;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Git;

public abstract class GitProviderBase
{
    protected GitProviderBase(string projectBase)
    {
        _projectBase = projectBase;
        _gitCli = new GitCommandLine(_projectBase);

        // "/" maps to _startDirectory
        _mapper = new PathMapper(_projectBase);
    }
    private readonly GitCommandLine _gitCli;
    protected readonly PathMapper _mapper;
    protected readonly string _projectBase;

    public List<WarningMessage> Warnings { get; } = [];

    /// <summary>
    ///     The start directory.
    /// </summary>
    public string BaseDirectory
    {
        get => _projectBase;
    }

    /// <summary>
    ///     <inheritdoc cref="ISourceControlProvider.CalculateDeveloperWork" />
    /// </summary>
    public Dictionary<string, uint> CalculateDeveloperWork(string localFile)
    {
        var annotate = _gitCli.Annotate(localFile);

        //S = not a whitespace
        //s = whitespace

        // Parse annotated file
        var workByDevelopers = new Dictionary<string, uint>();
        var changeSetRegex = new Regex(@"^\S+\t\(\s*(?<developerName>[^\t]+).*", RegexOptions.Multiline | RegexOptions.Compiled);

        // Work by change sets (line by line)
        var matches = changeSetRegex.Matches(annotate);
        foreach (Match match in matches)
        {
            var developer = match.Groups["developerName"].Value;
            developer = developer.Trim('\t');
            workByDevelopers.AddToValue(developer, 1);
        }

        return workByDevelopers;
    }

    public HashSet<string> GetAllTrackedFiles()
    {
        return GetAllTrackedFiles(null);
    }

    public HashSet<string> GetAllTrackedFiles(string? hash)
    {
        var serverPaths = _gitCli.GetAllTrackedFiles(hash);
        // NUL terminated and verbatim (ls-tree -z). Nothing to decode.
        var all = serverPaths.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return new HashSet<string>(all);
    }

    public List<string> GetAllTrackedLocalFiles()
    {
        var trackedServerPaths = GetAllTrackedFiles();

        // Filtered local paths
        return trackedServerPaths.Select(sp => _mapper.MapToLocalFile(sp)).ToList();
    }

    /// <summary>
    ///     Streams the whole history reachable from HEAD, newest first. See <see cref="GitLogReader" />.
    /// </summary>
    protected void ReadLog(Action<TextReader> readLog)
    {
        _gitCli.ReadLog(readLog);
    }

    protected int GetCommitCount()
    {
        return _gitCli.GetCommitCount();
    }


    protected Dictionary<string, Contribution> ExtractContributions(IProgress<string>? progress, IFilter fileTypeFilter)
    {
        var allLocalFiles = GetAllTrackedLocalFiles();

        var localFiles = allLocalFiles.Where(fileTypeFilter.IsAccepted).ToList();
        return CalculateContributionsParallel(progress, localFiles);
    }

    protected Dictionary<string, Contribution> CalculateContributionsParallel(IProgress<string>? progress, List<string> localFiles)
    {
        // Calculate main developer for each file
        var fileToContribution = new ConcurrentDictionary<string, Contribution>();

        var all = localFiles.Count;
        Parallel.ForEach(localFiles,
            file =>
            {
                var work = CalculateDeveloperWork(file);
                var contribution = new Contribution(work);

                if (work.Any()) // get rid of 0 byte files.
                {
                    var result = fileToContribution.TryAdd(file, contribution);
                    Debug.Assert(result);
                }

                // Progress
                var count = fileToContribution.Count;

                progress?.Report($"Calculating work {count}/{all}");
            });

        // Case-insensitive lookups are the contract. Git can track paths that differ only in
        // casing, so a plain ToDictionary could throw on duplicate keys - the helper keeps the
        // original casing, uses a case-insensitive comparer and deduplicates.
        return fileToContribution.ToCaseInsensitivePathKeys();
    }

    protected void VerifyGitPreConditions()
    {
        if (!Directory.Exists(Path.Combine(_projectBase, ".git")))
        {
            // We need the root (containing .git) because of the function MapToLocalFile.
            throw new ArgumentException("The given start directory is not the root of a git repository.");
        }

        if (!_gitCli.IsMasterGetCheckedOut())
        {
            // Branch may be developer or main etc.
            //throw new ArgumentException("The currently checked out branch is not the master branch.");
        }

        if (_gitCli.HasIndexOrWorkspaceChanges())
        {
            throw new ArgumentException("There are local changes that are not committed yet. This may give invalid results.");
        }
    }
}