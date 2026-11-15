using System.Text.Json.Serialization;
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
public sealed class GitProvider : GitProviderBase, ISourceControlProvider
{
    // Reporting every single commit floods the UI thread. The log is read much faster than it can be shown.
    private const int ProgressInterval = 100;

    public GitProvider(string projectBase) : base(projectBase)
    {
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

    public static string GetClass()
    {
        var type = typeof(GitProvider);
        return type.FullName + "," + type.Assembly.GetName().Name;
    }

    /// <summary>
    ///     The commits that changed a file existing at HEAD, newest first.
    ///     Commits that only touched files which are gone today are not part of the result.
    /// </summary>
    public ChangeSetHistory ExtractChangeSets(IProgress<string>? progress)
    {
        Warnings.Clear();

        var tracker = new FileIdentityTracker(GetAllTrackedFiles(), _mapper);
        var commitCount = GetCommitCount();
        var processed = 0;

        // Git prints the newest commit first. That is the order of the history contract, too.
        var changeSets = new List<ChangeSet>();

        ReadLog(reader =>
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
}
