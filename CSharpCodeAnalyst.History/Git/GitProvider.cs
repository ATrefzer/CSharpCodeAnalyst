using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CSharpCodeAnalyst.History.Extensions;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Git;

public class History
{
    public History(ChangeSetHistory changeSets)
    {
        ChangeSets = changeSets;
        Contributions = new Dictionary<string, Contribution>();
    }

    [JsonConstructor]
    public History(ChangeSetHistory changeSets, Dictionary<string, Contribution> contributions)
    {
        ChangeSets = changeSets;
        Contributions = contributions;
    }

    public bool HasContributions
    {
        get => Contributions.Any();
    }

    public ChangeSetHistory ChangeSets { get; }
    public Dictionary<string, Contribution> Contributions { get; private set; }

    /// <summary>
    ///     A JSON round-trip drops the case-insensitive comparer on the Contributions
    ///     dictionary. Restore it so path lookups behave like a freshly collected History.
    ///     Idempotent.
    /// </summary>
    public void NormalizeLookupKeys()
    {
        Contributions = Contributions.ToCaseInsensitivePathKeys();
    }
}

/// <summary>
///     Git provider with rename tracking.
///     Git does not know what a file is, only paths. A rename cuts the history of a file in two.
///
///     1. A single "git log" call delivers the whole history: every commit with its changed
///     files, the renames git detects and, for a merge, the diff against each parent.
///     2. The <see cref="FileIdentityTracker" /> walks this log from HEAD back to the first
///     commit and assigns each change to the file of today it belongs to.
///
///     The result only contains files that exist at HEAD. Every item carries the id of its file,
///     whatever the file was called when the commit was made.
///     See Documentation/Design/git-history-provider.md.
/// </summary>
public sealed class GitProvider
{
    // Reporting every single commit floods the UI thread. The log is read much faster than it can be shown.
    private const int ProgressInterval = 100;

    private readonly GitCommandLine _gitCli;
    private readonly PathMapper _mapper;
    private readonly string _projectBase;

    public GitProvider(string projectBase)
    {
        _projectBase = projectBase;
        _gitCli = new GitCommandLine(_projectBase);

        // "/" maps to _projectBase
        _mapper = new PathMapper(_projectBase);
    }

    public List<WarningMessage> Warnings { get; } = [];

    /// <summary>
    ///     The start directory.
    /// </summary>
    public string BaseDirectory
    {
        get => _projectBase;
    }

    public History ExtractHistory(IProgress<string>? progress, bool includeWorkData, IFilter fileTypeFilter)
    {
        VerifyGitPreConditions();
        var changeSets = ExtractChangeSets(progress);

        if (includeWorkData)
        {
            var contribution = ExtractContributions(progress, fileTypeFilter);
            return new History(changeSets, contribution);
        }

        return new History(changeSets);
    }

    /// <summary>
    ///     Developer name -> number of lines of the given file last modified by this developer.
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

    /// <summary>
    ///     The server paths of all files tracked at HEAD.
    /// </summary>
    public HashSet<string> GetAllTrackedFiles()
    {
        // NUL terminated and verbatim (ls-tree -z). Nothing to decode.
        var serverPaths = _gitCli.GetAllTrackedFiles();
        var all = serverPaths.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return [.. all];
    }

    public List<string> GetAllTrackedLocalFiles()
    {
        var trackedServerPaths = GetAllTrackedFiles();
        return trackedServerPaths.Select(sp => _mapper.MapToLocalFile(sp)).ToList();
    }

    /// <summary>
    ///     The commits that changed a file existing at HEAD, newest first.
    ///     Commits that only touched files which are gone today are not part of the result.
    /// </summary>
    public ChangeSetHistory ExtractChangeSets(IProgress<string>? progress)
    {
        Warnings.Clear();

        var tracker = new FileIdentityTracker(GetAllTrackedFiles(), _mapper);
        var commitCount = _gitCli.GetCommitCount();
        var processed = 0;

        // Git prints the newest commit first. That is the order of the history contract, too.
        var changeSets = new List<ChangeSet>();

        _gitCli.ReadLog(reader =>
        {
            foreach (var commit in GitLogReader.Read(reader))
            {
                processed++;
                if (processed % ProgressInterval == 0)
                {
                    progress?.Report($"Processing commit {processed} / {commitCount}");
                }

                var changeSet = tracker.Apply(commit);
                if (changeSet != null)
                {
                    changeSets.Add(changeSet);
                }
            }
        });

        Warnings.AddRange(tracker.Warnings);

        // Sanity checks. Neither is expected to happen.
        if (processed < commitCount)
        {
            Warnings.Add(new WarningMessage("HEAD", $"The git log was not read completely: {processed} of {commitCount} commits."));
        }

        foreach (var file in tracker.GetFilesWithoutHistory())
        {
            Warnings.Add(new WarningMessage("HEAD", $"No commit found for {file}."));
        }

        return new ChangeSetHistory(changeSets);
    }

    private Dictionary<string, Contribution> ExtractContributions(IProgress<string>? progress, IFilter fileTypeFilter)
    {
        var allLocalFiles = GetAllTrackedLocalFiles();

        var localFiles = allLocalFiles.Where(fileTypeFilter.IsAccepted).ToList();
        return CalculateContributionsParallel(progress, localFiles);
    }

    private Dictionary<string, Contribution> CalculateContributionsParallel(IProgress<string>? progress, List<string> localFiles)
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

    private void VerifyGitPreConditions()
    {
        if (!Directory.Exists(Path.Combine(_projectBase, ".git")))
        {
            // We need the root (containing .git) because of the function MapToLocalFile.
            throw new ArgumentException("The given start directory is not the root of a git repository.");
        }

        if (_gitCli.HasIndexOrWorkspaceChanges())
        {
            throw new ArgumentException("There are local changes that are not committed yet. This may give invalid results.");
        }
    }
}
