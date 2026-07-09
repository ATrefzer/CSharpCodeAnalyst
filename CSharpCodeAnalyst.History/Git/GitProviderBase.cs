using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
        var all = serverPaths.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return new HashSet<string>(all.Select(Decoder.DecodeEscapedBytes));
    }

    public List<string> GetAllTrackedLocalFiles()
    {
        var trackedServerPaths = GetAllTrackedFiles();

        // Filtered local paths
        return trackedServerPaths.Select(sp => _mapper.MapToLocalFile(sp)).ToList();
    }

    protected string GetHeadHash()
    {
        return _gitCli.GetHeadHash();
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

    protected ChangeSetHistory ParseLogString(string gitLogString)
    {
        var parser = new Parser(_mapper);
        return parser.ParseLogStringNoGraph(gitLogString);
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
    
    private string GetPathToExportedFile(DirectoryInfo cache, FileInfo localFile, string revision)
    {
        var name = new StringBuilder();

        // Stable hash. string.GetHashCode is randomized per process on .NET (Core),
        // which would defeat the cache on every restart.
        name.Append(GetStableHash(localFile.FullName));
        name.Append("_");
        name.Append(revision);
        name.Append("_");
        name.Append(localFile.Name);

        return Path.Combine(cache.Name, name.ToString());
    }

    private static string GetStableHash(string text)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant()));
            return Convert.ToHexString(bytes, 0, 8);
        }
    }
    

    protected void SaveFullLogToDisk(string path)
    {
        var log = _gitCli.Log();
        File.WriteAllText(Path.Combine(), log);
    }
}