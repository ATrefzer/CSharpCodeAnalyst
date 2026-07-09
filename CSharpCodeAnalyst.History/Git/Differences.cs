using LibGit2Sharp;

namespace CSharpCodeAnalyst.History.Git
{
    public sealed class Differences
    {
        /// <summary>
        /// Diff of the commit tree to each parent tree. Index 0 is the parent we merge into.
        /// A root commit has a single entry: the diff against the empty tree.
        /// </summary>
        public IReadOnlyList<List<TreeEntryChanges>> DiffsToParents { get; }

        /// <summary>
        /// Changes done in the commit itself. For a regular commit this is simply the diff
        /// to the parent. For a merge commit these are the files that differ from ALL
        /// parents (conflict resolutions, "evil merges"). We have to compare by path:
        /// TreeEntryChanges has no value equality, and the old blob ids differ per parent.
        /// </summary>
        public List<TreeEntryChanges> ChangesInCommit { get; }

        /// <summary>
        /// Changes relative to the first parent that came in from the merged branches.
        /// Empty for a regular commit.
        /// </summary>
        public List<TreeEntryChanges> DiffExclusiveToParent1 { get; }

        public Differences(List<TreeEntryChanges> diffToParent)
            : this(new List<List<TreeEntryChanges>> { diffToParent ?? new List<TreeEntryChanges>() })
        {
        }

        public Differences(List<TreeEntryChanges> deltaToParent1, List<TreeEntryChanges> deltaToParent2)
            : this(new List<List<TreeEntryChanges>>
                   {
                           deltaToParent1 ?? new List<TreeEntryChanges>(),
                           deltaToParent2 ?? new List<TreeEntryChanges>()
                   })
        {
        }

        public Differences(IReadOnlyList<List<TreeEntryChanges>> diffsToParents)
        {
            DiffsToParents = diffsToParents;

            var diffToParent1 = diffsToParents[0];

            // Paths that differ from every parent. For a single parent this is the whole diff.
            var changedInCommit = new HashSet<string>(diffToParent1.Select(change => change.Path));
            foreach (var diffToParent in diffsToParents.Skip(1))
            {
                changedInCommit.IntersectWith(diffToParent.Select(change => change.Path));
            }

            ChangesInCommit = diffToParent1.Where(change => changedInCommit.Contains(change.Path)).ToList();
            DiffExclusiveToParent1 = diffToParent1.Where(change => !changedInCommit.Contains(change.Path)).ToList();
        }
    }
}
