using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Git;

/// <summary>
///     Gives every file that exists at HEAD an id and finds the commits that changed it,
///     no matter what the file was called at that time.
///
///     The commits are fed newest first (children before parents). The tracker keeps a single
///     table "name -> id" that answers one question: if an older commit mentions this name,
///     which of today's files is meant? It starts with the files at HEAD and is rewound
///     commit by commit:
///
///     - Renamed old -> new: before this commit the file was called "old".
///     - Deleted: before this commit the name belonged to a file that is gone today.
///     - Added, modified: the name keeps its meaning.
///
///     So a name only changes its meaning where the history says it was given up. An add says
///     nothing about the older history of a name: the same file may have been added on another
///     branch as well (cherry-pick), and those commits count, too.
///
///     There is one table for the whole commit graph, not one per branch. This is what keeps
///     the algorithm small, and it is an approximation: see Documentation/Design/git-history-provider.md.
/// </summary>
public sealed class FileIdentityTracker
{
    private readonly PathMapper _mapper;

    // What a name means in the part of the history we have not seen yet (older commits).
    private readonly Dictionary<string, string> _nameToId = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _idToHeadPath = new();
    private readonly HashSet<string> _idsWithHistory = [];

    // Ids we have seen an add for. Used to tell a reused name from a real ambiguity.
    private readonly HashSet<string> _idsSeenAdded = [];

    public FileIdentityTracker(IEnumerable<string> filesAtHead, PathMapper mapper)
    {
        _mapper = mapper;
        foreach (var path in filesAtHead)
        {
            var id = Guid.NewGuid().ToString();
            _nameToId[path] = id;
            _idToHeadPath[id] = path;
        }
    }

    public List<WarningMessage> Warnings { get; } = [];

    /// <summary>
    ///     Processes the next (older) commit.
    ///     Returns the change set with the files of today the commit changed, or null if there are none.
    /// </summary>
    public ChangeSet? Apply(LogCommit commit)
    {
        // Attribute first: the changes are described with the names valid after the commit.
        var items = CreateItems(commit);

        RewindNames(commit);

        if (items.Count == 0)
        {
            return null;
        }

        var changeSet = new ChangeSet
        {
            Id = commit.Hash,
            Comment = commit.Subject,
            Committer = commit.Author,
            Date = commit.Date
        };
        changeSet.Items.AddRange(items);
        return changeSet;
    }

    /// <summary>
    ///     Files at HEAD no commit was found for. Call after the last commit.
    /// </summary>
    public List<string> GetFilesWithoutHistory()
    {
        return _idToHeadPath
            .Where(pair => !_idsWithHistory.Contains(pair.Key))
            .Select(pair => pair.Value)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private List<ChangeItem> CreateItems(LogCommit commit)
    {
        var items = new List<ChangeItem>();
        var idsInCommit = new HashSet<string>();

        foreach (var change in GetOwnChanges(commit))
        {
            if (change.Kind is KindOfChange.Delete or KindOfChange.None)
            {
                // A deleted file is not one of today's files.
                continue;
            }

            if (!_nameToId.TryGetValue(change.Path, out var id))
            {
                continue;
            }

            if (change.Kind is KindOfChange.Add or KindOfChange.Copy)
            {
                _idsSeenAdded.Add(id);
            }

            if (!idsInCommit.Add(id))
            {
                // Two names of the same file in one commit. Count the commit once.
                continue;
            }

            _idsWithHistory.Add(id);
            items.Add(new ChangeItem
            {
                Id = id,
                Kind = change.Kind,
                ServerPath = change.Path,
                FromServerPath = change.FromPath ?? change.Path,
                LocalPath = _mapper.MapToLocalFile(change.Path)
            });
        }

        return items;
    }

    /// <summary>
    ///     The changes made in the commit itself.
    ///     For a regular commit this is its diff. A merge commit only counts for the files that
    ///     differ from every parent. Everything else merely arrives from one of the branches
    ///     and was already counted at the commit that did the work.
    /// </summary>
    private static IEnumerable<LogChange> GetOwnChanges(LogCommit commit)
    {
        if (!commit.IsMerge)
        {
            return commit.Diffs.SelectMany(diff => diff);
        }

        if (commit.Diffs.Count < commit.Parents.Count)
        {
            // Identical to at least one parent, so nothing differs from all of them.
            // In other words: If we have changes in this merge commit itself, we are different to all other parents.
            return [];
        }

        // Files that are changed in the merge commit itself. These files are different from every parent, and in each diff!
        var changedAgainstOtherParents = commit.Diffs
            .Skip(1)
            .Select(diff => diff.Select(change => change.Path).ToHashSet(StringComparer.Ordinal))
            .ToList();

        return commit.Diffs[0].Where(change => changedAgainstOtherParents.All(paths => paths.Contains(change.Path)));
    }

    private void RewindNames(LogCommit commit)
    {
        // Name -> id of the file that had the name before the commit. Null: a file that is gone.
        // Collect first and apply afterwards. All entries of a commit happen at once.
        var before = new Dictionary<string, string?>(StringComparer.Ordinal);

        // A merge is compared to each parent. All of these diffs tell us names that are not
        // in use after the commit. This is how the name a file has on the other branch is
        // known before the first commit of that branch arrives.
        foreach (var change in commit.Diffs.SelectMany(diff => diff))
        {
            if (change.Kind == KindOfChange.Delete)
            {
                before.TryAdd(change.Path, null);
            }
            else if (change.Kind == KindOfChange.Rename && change.FromPath != null)
            {
                if (_nameToId.TryGetValue(change.Path, out var id))
                {
                    // A rename tells more than a delete of the same name against another parent.
                    before[change.FromPath] = id;
                }
                else
                {
                    before.TryAdd(change.FromPath, null);
                }
            }
        }

        foreach (var (name, id) in before)
        {
            if (id == null)
            {
                _nameToId.Remove(name);
                continue;
            }

            WarnIfAmbiguous(commit, name, id);
            _nameToId[name] = id;
        }
    }

    private void WarnIfAmbiguous(LogCommit commit, string name, string id)
    {
        if (!_nameToId.TryGetValue(name, out var currentId) || currentId == id)
        {
            return;
        }

        if (_idsSeenAdded.Contains(currentId))
        {
            // Not ambiguous: the name was reused. The other file was added after this rename.
            return;
        }

        // Two of today's files go back to the same name, for example a file renamed
        // differently on two branches. Only one of them can inherit the older history.
        Warnings.Add(new WarningMessage(commit.Hash,
            $"'{name}' is the origin of both '{_idToHeadPath[currentId]}' and '{_idToHeadPath[id]}'. " +
            $"The older history is assigned to '{_idToHeadPath[id]}'."));
    }
}
