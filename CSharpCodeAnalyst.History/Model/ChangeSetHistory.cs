using System.Diagnostics;
using CSharpCodeAnalyst.History.Config;

namespace CSharpCodeAnalyst.History.Model
{
    [Serializable]
    public sealed class ChangeSetHistory
    {
        public ChangeSetHistory(List<ChangeSet> changeSets)
        {
            ChangeSets = changeSets;
        }

        public List<ChangeSet> ChangeSets { get; }

        public void CleanupHistory()
        {
            CleanupHistory(this);
        }

        /// <summary>
        ///     Returns a flat summary of all artifacts found in the commit history.
        /// </summary>
        public List<Artifact> GetArtifactSummary(IFilter filter, IAliasMapping aliasMapping)
        {
            // Item id -> artifact
            var artifacts = new Dictionary<string, Artifact>();

            var set = new HashSet<string>();

            // Files we already know we skip are not checked again!
            var ignoredIds = new HashSet<string>();

            foreach (var changeset in ChangeSets)
            {
                if (changeset.WorkItems.Count >= Thresholds.MaxWorkItemsPerCommitForSummary)
                {
                    // Ignore monster merges.
                    // Note: We may lose files for the summary when the last merge with many work items contains a final rename.
                    // Maybe write a warning or make further analysis.
                    continue;
                }

                Debug.Assert(set.Add(changeset.Id)); // Change set appears only once
                foreach (var item in changeset.Items)
                {
                    // The first time we see a file (id) it is the latest version of the file.
                    // Either add it to the summary or ignore list.
                    var id = item.Id;
                    if (ignoredIds.Contains(id))
                    {
                        // Files we already know to be skipped are not checked again! (Performance)
                        continue;
                    }

                    if (!artifacts.ContainsKey(id))
                    {
                        // The changeset where we see the item the first time is the latest revision!
                        // Whether we track this file at all is decided here, once, using its current
                        // (= latest) path. Older items of the same id often carry a path from before
                        // a rename, which by now no longer exists on disk and would never pass the
                        // filter or the Exists() check - that must not throw away commits we already
                        // counted for this id.

                        if (filter != null && !filter.IsAccepted(item.LocalPath))
                        {
                            ignoredIds.Add(id);
                            continue;
                        }

                        if (!Exists(item))
                        {
                            // This may still happen because we skip large merges that may contain a final rename.
                            // So we have a code metric but still believe that the file is at its former location

                            Trace.WriteLine($"Ignored file: '{item.LocalPath}'. It should exist. Possible cause: Ignored commit with too much work items containing a final rename.");
                            ignoredIds.Add(id);
                            continue;
                        }

                        artifacts[id] = CreateArtifact(changeset, item);
                    }
                    else
                    {
                        // Changesets seen first are expected so have newer dates.
                        Debug.Assert(artifacts[id].Date >= changeset.Date);
                    }

                    var artifact = artifacts[id];
                    var committerAlias = aliasMapping.GetAlias(changeset.Committer);

                    // Aggregate information from earlier commits (for example number of commits etc)
                    ApplyCommits(artifact);
                    ApplyCommitter(artifact, committerAlias);
                    ApplyWorkItems(artifact, changeset);
                }
            }

            // Remove entries that exist on hard disk but are removed form TFS!
            // Flatten the structure and return only the artifacts.
            return artifacts.Where(pair => !pair.Value.IsDeleted).Select(pair => pair.Value).ToList();
        }

        private static void ApplyCommitter(Artifact artifact, string committer)
        {
            artifact.Committers.Add(committer);
        }

        private static void ApplyCommits(Artifact artifact)
        {
            artifact.Commits = artifact.Commits + 1;
        }

        private static void ApplyWorkItems(Artifact artifact, ChangeSet changeset)
        {
            foreach (var workitem in changeset.WorkItems)
            {
                artifact.WorkItems.Add(workitem);
            }
        }

        private static Artifact CreateArtifact(ChangeSet cs, ChangeItem item)
        {
            Debug.Assert(item.LocalPath != null);
            var artifact = new Artifact
                           {
                                   Id = item.Id,
                                   LocalPath = item.LocalPath,
                                   ServerPath = item.ServerPath,
                                   Commits = 0,

                                   // Item used to create sets the revision (latest)
                                   Revision = cs.Id,

                                   // Assume first item is latest revision. If this is deleted the item should no longer be on hard disk.
                                   IsDeleted = item.IsDelete(),

                                   Date = cs.Date
                           };

            return artifact;
        }

        /// <summary>
        /// Removes all non tracked files.
        /// Note that after this step we still may find Delete actions that were not merged.
        /// So the function by default drops all deleted items.
        /// </summary>
        public void CleanupHistory(HashSet<string> aliveIds, bool dropDeletes = true)
        {
            foreach (var set in ChangeSets)
            {
                set.Items.RemoveAll(item => !aliveIds.Contains(item.Id));
                if (dropDeletes)
                {
                    set.Items.RemoveAll(item => item.IsDelete());
                }
            }

            ClearEmptyCommits(this);
        }

        /// <summary>
        /// Removes all files that were deleted and are no longer available
        /// </summary>
        private void CleanupHistory(ChangeSetHistory history)
        {
            var deleted = history.ChangeSets
                                        .SelectMany(set => set.Items)
                                        .Where(item => item.IsDelete())
                                        .Select(item => item.Id);

            var deletedIdsHash = new HashSet<string>(deleted);

            foreach (var set in history.ChangeSets)
            {
                set.Items.RemoveAll(item => deletedIdsHash.Contains(item.Id));
            }

            ClearEmptyCommits(history);
        }

        private static void ClearEmptyCommits(ChangeSetHistory history)
        {
            // Delete empty commits
            var changeSetsCopy = history.ChangeSets.ToList();
            foreach (var changeSet in changeSetsCopy)
            {
                if (!changeSet.Items.Any())
                {
                    history.ChangeSets.Remove(changeSet);
                }
            }
        }

        private bool Exists(ChangeItem item)
        {
            return item.Exists();
        }

    }
}