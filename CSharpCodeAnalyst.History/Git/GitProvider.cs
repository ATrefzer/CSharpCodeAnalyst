using System.Diagnostics;
using System.Text.Json.Serialization;
using CSharpCodeAnalyst.History.Extensions;
using CSharpCodeAnalyst.History.Model;
using LibGit2Sharp;

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
///     Git is not intended to track renames. So there are some tradeoffs.
/// 
///     1. First I read the full git graph. This is quite fast even for a larger repository.
///     2. Then I process the nodes from initial node to head. Following a breadth first search.
///     I initialize a scope (file name -> unique id) with all initial tracked files.
///     3. Following the graph I update the scopes to reflect all file operations (add, edit etc)
///     Operations that are easy to track like a simple rename in a branch and then merge again
///     are tracked. As soon I encounter a situation that is not clear I reset tracking by
///     assigning a new Id to the file.
///     I found a lot of these situations when parsing the NUnit repository as a test.
/// 
///     The algorithm is quite slow. NUnit can be parsed in less than a minute. But for smaller
///     repositories (like NUnit) this gives more interesting results than without tracking renames.
/// 
/// </summary>
public sealed class GitProvider : GitProviderBase, ISourceControlProvider
{

    private Statistics _stats =  new Statistics();

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

    private ChangeSetHistory ExtractChangeSets(IProgress<string>? progress)
    {
        var (history, graph) = GetRawHistory(progress);

        // Remove deleted files and empty changes sets
        var headNode = graph.GetNode(GetHeadHash());

        // Verify first. If the scope drifted from the tracked files we get warnings here.
        VerifyScope(headNode);

        var head = headNode!;
        var scope = head.Scope!;

        // The scope may not contain every tracked file (see warnings above),
        // so we must not use GetId here. It would throw.
        var allTrackedFiles = GetAllTrackedFiles();
        var aliveIds = new HashSet<string>(allTrackedFiles
            .Select(scope.GetIdOrDefault)
            .Where(id => id != null)!);

        // Note we have to drop all Delete items from the history. This is safe.
        // A file can be deleted in one branch but maintained in another one.
        // If we would call CleanupHistory() we would remove all ids that belong to a deleted file
        // This means we lose a file that is still tracked.

        history.CleanupHistory(aliveIds);
        Debug.Assert(!history.ChangeSets.SelectMany(cs => cs.Items).Any(item => item.IsDelete()));

        return history;
    }

    /// <summary>
    ///     Raw history. Nothing deleted or cleaned
    /// </summary>
    public (ChangeSetHistory, Graph) GetRawHistory(IProgress<string>? progress)
    {
        // GetRawHistory is also called directly (tests), so initialize here.
        Warnings.Clear();
        _stats = new Statistics();

        Graph graph;
        ChangeSetHistory history;
        using (var repo = new Repository(_projectBase))
        {
            // All nodes in current branch reachable from the head.

            progress?.Report("Reading commit graph");
            graph = GetGraph(repo);

            progress?.Report("Creating the history");
            history = CreateHistory(repo, graph, progress);
        }

        return (history, graph);
    }


    private bool IsMergeWithUnprocessedParents(GraphNode node)
    {
        // We can't process a merge commit before all parents are done. But we end up here again.
        return IsMerge(node) && node.Parents.Any(parent => parent.Scope == null);
    }

    /// <summary>
    ///     Creates the history in a form that we can process further from initial commit to the last one.
    /// </summary>
    private ChangeSetHistory CreateHistory(Repository repo, Graph graph, IProgress<string>? progress)
    {
        var nodeCount = graph.AllNodes.Count();
        var currentNode = 0;

        var changeSets = new List<ChangeSet>();

        // A repository can have more than one root commit, for example when
        // unrelated histories were merged. Start the traversal from all of them.
        // Otherwise the merge commit joining the histories waits forever for the
        // scope of a parent that is never processed.
        var initialNodes = graph.AllNodes.Where(node => node.Parents.Any() is false).ToList();
        Debug.Assert(initialNodes.Count >= 1);

        var alreadyProcessed = new HashSet<string>();
        var nodesToProcess = new Queue<GraphNode>();
        foreach (var initialNode in initialNodes)
        {
            nodesToProcess.Enqueue(initialNode);
        }

        while (nodesToProcess.Any())
        {
            var node = nodesToProcess.Dequeue();

            if (IsMergeWithUnprocessedParents(node))
            {
                // We arrive here later again.
                continue;
            }

            if (!alreadyProcessed.Add(node.CommitHash))
            {
                // When we arrive a merge node the first time both parents may be complete.
                // In this case we would end up following the same path twice.
                continue;
            }

            progress?.Report($"Processing commit {++currentNode} / {nodeCount}");

            var commit = repo.Lookup<Commit>(node.CommitHash);
            var differences = CalculateDiffs(repo, commit);

            var (scope, deletedServerPathToId) = ApplyChangesToScope(node, differences);
            node.Scope = scope;


            var cs = CreateChangeSet(commit);
            changeSets.Add(cs);

            // Create change items
            foreach (var change in differences.ChangesInCommit)
            {
                var item = CreateChangeItem(change, scope, deletedServerPathToId, node.CommitHash);
                cs.Items.Add(item);
            }

            // Add children to processing queue
            foreach (var childNode in node.Children)
            {
                nodesToProcess.Enqueue(childNode);
            }
        }

        // The change sets were created in topological order (a commit is processed only after
        // all of its parents). The history contract is newest first, so reverse the list.
        // Sorting by date is not reliable: git stores timestamps with second precision and
        // rebased commits may carry out of order dates.
        changeSets.Reverse();
        var history = new ChangeSetHistory(changeSets);
        return history;
    }

    /// <summary>
    ///     Returns the deleted files, no longer in scope (server path -> id)
    /// </summary>
    private (Scope, Dictionary<string, string>) ApplyChangesToScope(GraphNode node, Differences deltas)
    {
        var deletedServerPathToId = new Dictionary<string, string>();
        Scope? scope = null;

        if (node.Parents.Count == 0)
        {
            // Called once for each root commit
            scope = new Scope();
            UpdateScopeSingleOrNoParent(deltas, scope, deletedServerPathToId, node.CommitHash);
        }
        else if (node.Parents.Count == 1)
        {
            var parent = node.Parents.Single();
            Debug.Assert(parent.Scope != null);

            if (parent.Children.Count > 1)
            {
                // We follow two branches, so each one gets its own copy.
                scope = parent.Scope.Clone();
            }
            else
            {
                // Safe to reuse: a parent with a single child is never read again.
                scope = parent.Scope;
            }

            UpdateScopeSingleOrNoParent(deltas, scope, deletedServerPathToId, node.CommitHash);
        }
        else
        {
            Debug.Assert(IsMerge(node));

            var mergeInto = node.Parents[0];
            var mergeFromScopes = node.Parents.Skip(1).Select(parent => parent.Scope).ToList();

            // We have to clone the merge scope. The same node may be processed later again as a parent node. Therefore, we
            // have to keep its scope untouched.
            var mergeIntoScope = mergeInto.Scope.Clone();

            scope = mergeIntoScope;

            UpdateScopeMergeParents(deltas, mergeIntoScope, mergeFromScopes, deletedServerPathToId, node.CommitHash);
        }

        return (scope, deletedServerPathToId);
    }


    private void UpdateScopeSingleOrNoParent(Differences deltas, Scope scope, Dictionary<string, string> deleted, string commitHash)
    {
        foreach (var change in deltas.ChangesInCommit)
        {
            ApplyChangesToScope(scope, change, deleted, commitHash);
        }
    }

    /// <summary>
    ///     Update the scope of mergeInto to reflect the changes made in the branch(es) we merge from.
    ///     When files are removed I collect them in the "deleted" dictionary.
    ///     These are needed later to assign the id to the deleted file.
    /// </summary>
    private void UpdateScopeMergeParents(Differences deltas, Scope mergeIntoScope, IReadOnlyList<Scope> mergeFromScopes, Dictionary<string, string> deleted, string commitHash)
    {
        foreach (var change in deltas.DiffExclusiveToParent1)
        {
            // These are the changes done on the feature branch(es). We merge them into the scope.
            UpdateScopeFromMergeSource(mergeIntoScope, mergeFromScopes, change, deleted, commitHash);
        }

        foreach (var change in deltas.ChangesInCommit)
        {
            // These are the changes done on the merge commit itself (fixing conflicts etc)
            ApplyChangesToScope(mergeIntoScope, change, deleted, commitHash);
        }
    }

    private void UpdateScopeFromMergeSource(Scope mergeIntoScope, IReadOnlyList<Scope> mergeFromScopes, TreeEntryChanges change,
        IDictionary<string, string> deleted, string commitHash)
    {
        // Merge changes from feature branch. Apply all Ids we can find without ambiguity.

        if (change.Status == ChangeKind.Added)
        {
            // The file may come from any of the merged branches. If they disagree
            // about the id the situation is ambiguous and we reset tracking below.
            var idsFrom = mergeFromScopes
                .Select(mergeFromScope => mergeFromScope.GetIdOrDefault(change.Path))
                .Where(id => id != null)
                .Distinct()
                .ToList();
            var idFrom = idsFrom.Count == 1 ? idsFrom[0] : null;

            var idInto = mergeIntoScope.GetIdOrDefault(change.Path);
            if (idFrom != null && idInto == null &&
                mergeIntoScope.GetServerPathOrDefault(Guid.Parse(idFrom)) == null)
            {
                // Take the Id from the (feature) branch where the file was added.

                // File is known in "from scope" but not in "into scope" and the id in "from scope" is not in "into scope"
                mergeIntoScope.MergeAdd(change.Path, Guid.Parse(idFrom));
                return;
            }

            if (idFrom != null && idFrom == idInto &&
                mergeIntoScope.GetServerPath(Guid.Parse(idInto)) == change.Path)
            {
                // Nothing to update. File existed in both parents with same id and same name.
                return;
            }

            // In all other cases reset tracking
            ResetTracking(mergeIntoScope, change.Path, commitHash);
        }
        else if (change.Status == ChangeKind.Modified || change.Status == ChangeKind.TypeChanged)
        {
            // Id must be known in main branch.
            Debug.Assert(mergeIntoScope.IsKnown(change.Path));
        }
        else if (change.Status == ChangeKind.Deleted)
        {
            var id = mergeIntoScope.GetIdOrDefault(change.Path);
            if (id != null)
            {
                mergeIntoScope.Remove(change.Path);
                deleted.Add(change.Path, id);
            }
        }
        else if (change.Status == ChangeKind.Renamed)
        {
            if (mergeIntoScope.IsKnown(change.OldPath) is false)
            {
                ResetTracking(mergeIntoScope, change.Path, commitHash);
            }
            else
            {
                mergeIntoScope.Update(change.OldPath, change.Path);
            }
        }
        else if (change.Status == ChangeKind.Copied)
        {
            // The copy source is ambiguous in a merge. The target is a new start.
            ResetTracking(mergeIntoScope, change.Path, commitHash);
        }
        else
        {
            // Should not appear in a committed tree diff. Do not crash the whole sync.
            Debug.Fail($"Not handled: {change.Status}");
            ResetTracking(mergeIntoScope, change.Path, commitHash);
        }
    }

    /// <summary>
    ///     We can no longer follow the identity of the file. It starts over with a new id.
    /// </summary>
    private void ResetTracking(Scope scope, string serverPath, string commitHash)
    {
        scope.Remove(serverPath);
        scope.Add(serverPath);
        _stats.ResetRenameTrackingOnFile++;
        Warnings.Add(new WarningMessage(commitHash, $"Reset file rename tracking for {serverPath}."));
    }

    private void ApplyChangesToScope(Scope scope, TreeEntryChanges change, IDictionary<string, string> deleted, string commitHash)
    {
        // Single parent or changes done in a merge commit itself.

        if (change.Status == ChangeKind.Added)
        {
            scope.Add(change.Path);
        }
        else if (change.Status == ChangeKind.Modified || change.Status == ChangeKind.TypeChanged)
        {
            // Scope is not affected
        }
        else if (change.Status == ChangeKind.Deleted)
        {
            var id = scope.GetIdOrDefault(change.Path);
            if (id != null)
            {
                scope.Remove(change.Path);
                deleted.Add(change.Path, id);
            }
            else
            {
                // Scope drift: the file was not tracked, so there is nothing to remove.
                Warnings.Add(new WarningMessage(commitHash, $"Deleted file was not tracked: {change.Path}"));
            }
        }
        else if (change.Status == ChangeKind.Renamed)
        {
            if (scope.IsKnown(change.OldPath) is false)
            {
                // Scope drift: we don't know the source of the rename.
                ResetTracking(scope, change.Path, commitHash);
            }
            else
            {
                scope.Update(change.OldPath, change.Path);
            }
        }
        else if (change.Status == ChangeKind.Copied)
        {
            // A copy is a new start for the target file.
            scope.Add(change.Path);
        }
        else
        {
            // Conflicted, Unreadable etc. should not appear in a committed tree diff.
            Debug.Fail($"Not handled: {change.Status}");
            ResetTracking(scope, change.Path, commitHash);
        }
    }

    private ChangeItem CreateChangeItem(TreeEntryChanges change, Scope scope,
        Dictionary<string, string> deletedServerPathToId, string commitHash)
    {
        var id = scope.GetIdOrDefault(change.Path);
        var item = new ChangeItem
            {
                Kind = ToChangeKind(change.Status),
                ServerPath = change.Path,
                FromServerPath = change.OldPath,
                LocalPath = _mapper.MapToLocalFile(change.Path),
                // Assign id
                Id = id
            };


        if (item.Id == null)
        {
            Debug.Assert(change.Status == ChangeKind.Deleted);
            if (deletedServerPathToId.TryGetValue(change.Path, out var deletedId) is false)
            {
                // Scope drift: we never tracked this file. Use a one time id.
                // CleanupHistory drops the item because the id is not alive at head.
                deletedId = Guid.NewGuid().ToString();
                Warnings.Add(new WarningMessage(commitHash, $"No id found for {change.Path}. Using a one time id."));
            }

            item.Id = deletedId;
        }


        return item;
    }

    private static ChangeSet CreateChangeSet(Commit commit)
    {
        var cs = new ChangeSet
            {
                Comment = commit.MessageShort,
                Id = commit.Sha,
                Committer = commit.Author.Name,
                
                // Committer date, not author date. After a rebase or cherry-pick the author date
                // may be older than the parent commit and would break the history ordering.
                Date = commit.Committer.When.LocalDateTime
            };

        return cs;
    }

    /// <summary>
    ///     Calculates the difference of the commit tree to each parent tree.
    /// </summary>
    private Differences CalculateDiffs(Repository repo, Commit commit)
    {
        var options = new CompareOptions();

        var parents = commit.Parents.ToArray();

        if (parents.Length == 0)
        {
            // Root commit: diff against the empty tree.
            var diffToParent = repo.Diff.Compare<TreeChanges>(null, commit.Tree, options).ToList();
            return new Differences(diffToParent);
        }

        // Regular commits have one parent, merge commits two or more (octopus).
        var diffsToParents = parents
            .Select(parent => repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree, options).ToList())
            .ToList();

        return new Differences(diffsToParents);
    }

    /// <summary>
    ///     Getting the graph alone is quite fast. For NUnit repository it is less than 300ms
    /// </summary>
    private Graph GetGraph(Repository repo)
    {
        var graph = new Graph();
        var head = repo.Head.Tip;

        var processed = new HashSet<string>();
        var queue = new Queue<Commit>();
        queue.Enqueue(head);
        while (queue.Any())
        {
            var commit = queue.Dequeue();
            graph.UpdateGraph(commit.Sha, commit.Parents.Select(p => p.Sha));

            foreach (var parent in commit.Parents)
            {
                if (processed.Add(parent.Sha))
                {
                    queue.Enqueue(parent);
                }
            }
        }

        return graph;
    }

    private void VerifyScope(GraphNode? node)
    {
        if (node?.Scope == null)
        {
            throw new InvalidOperationException("Node has no scope assigned!");
        }

        var expectedServerPaths = GetAllTrackedFiles(node.CommitHash);
        var actualServerPaths = node.Scope.GetAllFiles();

        var differences = expectedServerPaths;
        differences.SymmetricExceptWith(actualServerPaths);
        foreach (var diff in differences)
        {
            Warnings.Add(new WarningMessage(node.CommitHash, $"Final scope does not line up with tracked files: {diff}"));
        }
    }

    private KindOfChange ToChangeKind(ChangeKind kind)
    {
        switch (kind)
        {
            case ChangeKind.Unmodified:
                break;
            case ChangeKind.Added:
                return KindOfChange.Add;
            case ChangeKind.Deleted:
                return KindOfChange.Delete;
            case ChangeKind.Modified:
                return KindOfChange.Edit;
            case ChangeKind.Renamed:
                return KindOfChange.Rename;
            case ChangeKind.Copied:
                return KindOfChange.Copy;
            case ChangeKind.TypeChanged:
                return KindOfChange.TypeChanged;
        }

        return KindOfChange.None;
    }

    private bool IsMerge(GraphNode graphNode)
    {
        // An octopus merge can have more than two parents.
        return graphNode.Parents.Count >= 2;
    }
}