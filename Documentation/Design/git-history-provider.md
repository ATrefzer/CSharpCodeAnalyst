# GitProvider — the history of a file across renames

`CSharpCodeAnalyst.History/Git/GitProvider.cs` answers the one question every history analysis (hotspots, change coupling, knowledge) is built on:

> *How often — and together with what — did **this file** change?*

Git cannot answer it directly. A commit is a snapshot of a tree and a "file" is just a path. A rename cuts the history of a file in two: an old, dead path with years of changes and a seemingly brand-new file. Long-lived files are exactly the ones most likely to have been moved, so counting per path loses the interesting hotspots.

The provider therefore gives every file that exists at `HEAD` an **id** and returns a `ChangeSetHistory` — commits, newest first, each with the files it changed — in which every item carries the id of its file, whatever the file was called when the commit was made. Everything downstream groups by this id.

It consists of three small parts:

| Part | Job |
|---|---|
| `GitCommandLine.ReadLog` | One `git log` call that delivers the whole history. |
| `GitLogReader` | Turns the output into `LogCommit`s. Pure text parsing. |
| `FileIdentityTracker` | Assigns each change to the file of today it belongs to. Pure, no git involved. |

## 1. One git call

```
git log --topo-order -m -M --name-status -z --format=<header>
```

| Option | Why |
|---|---|
| `-M` | Rename detection. Git does not store renames; it detects them when diffing (by default: at least 50 % of the content is the same). |
| `-m` | For a merge commit, print the diff against **each** parent. By default `git log` shows no diff for merges at all. |
| `--topo-order` | No commit is printed before all of its children. The tracker relies on it. |
| `-z` | Fields are NUL terminated and paths printed verbatim. Without it git quotes and escapes unusual paths. |

Everything that shapes the output is passed explicitly (`-c log.diffMerges=separate`, `-c log.showRoot=true`, `--no-color`, `--no-show-signature`, `--encoding=UTF-8`, `-c diff.renameLimit=…`). Otherwise the result would depend on the git configuration of the user — with `diff.renames=false` there would silently be no rename tracking at all. `GitProviderTests.ResultDoesNotDependOnTheGitConfiguration` pins this.

`git log --follow <file>` is not an alternative: it handles a single file per call and is unreliable across merges. One call per file is what made the very first version of this provider unusably slow.

### The output format

```
\x01 hash \x02 parents \x02 committer date \x02 author \x02 subject \x03 \0
\n status \0 path \0                 add, modify, delete, type change
   status \0 old path \0 path \0     rename, copy
```

For a merge, git repeats the header once per parent the merge **differs from**; `GitLogReader` folds these into one `LogCommit` with one diff per parent. A merge that is identical to one of its parents therefore has fewer diffs than parents — a fact the tracker uses.

The output is streamed, not read into one string. If the stream is cut off (git died), the reader drops the incomplete commit and stops; the non-zero exit code of git then turns into a `ProviderException`.

## 2. The idea: walk backwards and keep one table of names

The tracker reads the log from `HEAD` back to the first commit and keeps a single table:

> **name → id.** If an older commit mentions this name, which of today's files is meant?

It starts with the files at `HEAD` (each gets a fresh id) and is rewound commit by commit. A change is first assigned — its path is looked up in the table, and if it is there the commit gets an item for that id — then the table is updated:

| The commit says | What it means for older commits |
|---|---|
| Renamed `old` → `new` | Before this commit the file was called `old`: `old` now maps to the id of `new`. |
| Deleted `p` | Before this commit `p` was a file that is gone today: `p` is removed from the table. |
| Added, modified | Nothing. The name keeps its meaning. |

So a name only changes its meaning **where the history says it was given up**. Two consequences:

* A file deleted and later created again under the same path does not inherit the old history — the delete in between clears the name.
* An *add* says nothing about older commits. The same file may have been added on another branch as well (a cherry-pick, a back-port), and those commits count too.

Changes to a path that is not in the table belong to files that no longer exist. They are skipped, so there is no clean-up pass afterwards: a commit that only touched dead files simply produces no change set.

```
A  add     Utils/Helper.cs
B  edit    Utils/Helper.cs
C  rename  Utils/Helper.cs -> Core/TextHelper.cs
D  edit    Core/TextHelper.cs                         HEAD
```

Start: `Core/TextHelper.cs → id-1`. `D` is assigned to `id-1`. `C` is assigned to `id-1`, then the table becomes `Utils/Helper.cs → id-1`. `B` and `A` use that name and are assigned to `id-1` as well: four changes for one file.

## 3. Merges

A merge commit is used for two things.

**What counts as a change of the merge itself.** Only the files that differ from *every* parent. Everything else merely arrives from one of the branches and was already counted at the commit that did the work. A merge that just combines two branches therefore contributes nothing and is not part of the result.

> This includes files both branches changed and git merged automatically: the result differs from both parents. Git cannot tell an automatic merge from a hand-resolved conflict with these options. `git log --remerge-diff` could, at the price of re-doing every merge (about 5 s for 2 250 merges); it is not used.

**Learning names early.** There is one table for the whole commit graph, but on parallel branches a file can have different names at the same time. The diffs of the merge against its parents bridge that: a rename (or delete) against *any* parent names a path that is not in use after the merge, and the same rule as above is applied.

```
main:     A ---- C (edit Report.cs) ---- M
            \                          /
feature:     B (rename to ReportWriter.cs)
```

Compared to `C`, the merge shows `Report.cs → ReportWriter.cs`. So when `C` is processed — possibly before `B`, the order of the two branches is up to git — `Report.cs` already maps to the right id.

## 4. What it does not get right

One table for the whole graph is what keeps the tracker small; it is also an approximation. Rename detection is a heuristic to begin with, so exact bookkeeping on top of it buys less than it seems. The known cases:

* **A rename git does not detect.** If less than half of the content survives, git reports a delete and an add and the history is cut there. A branch that renames *and* rewrites a file is fine as long as the rename is its own commit (`RenamedAndRewrittenOnBranch_DoesNotDependOnTheFileNames`).
* **A delete that did not win.** A file deleted on one branch but kept by the merge: when the delete commit is processed, the name is cleared, and commits of the other branch that git prints later are missed.
* **Two files with the same origin.** A file renamed differently on two branches where both survive: only one can inherit the history before the split. This produces a warning naming both files.
* **A reused path on a parallel branch.** Commits of a dead file can be assigned to a new file of the same name if git prints them before the delete.

All of them lose or misplace a few commits of one file; none aborts the import. Two sanity checks produce warnings: a file at `HEAD` without any commit, and fewer commits read than `git rev-list --count HEAD` reports.

## 5. Why not one table per branch

Until November 2026 the provider did the exact thing: it read the commit graph with LibGit2Sharp, walked it forward from the root commits and kept a `path ↔ id` table (a *scope*) per commit, cloned at every branch point and merged at every merge commit, resetting an id whenever two branches disagreed.

It was replaced because the simple version is faster, smaller and — measured on real repositories — loses less history:

| Repository | Commits / merges | Before | Now | Files losing commits compared to plain path counting (before → now) |
|---|---|---|---|---|
| CSharpCodeAnalyst | 708 / 24 | 1–3 s, 18 MB | 0.7 s, 6 MB | 1 → 1 |
| MiKo-Analyzers | 7 703 / 2 250 | 14 s, 717 MB | 1.1 s, 18 MB | 25 → 7 |
| colibri | 5 150 / 1 793 | 7 s, 271 MB | 1.4 s, 14 MB | 47 → 2 |

For about 96–97 % of the files both versions find exactly the same commits. The differences come from the rename heuristics (libgit2 and git do not agree on borderline cases) and from the cases where the old version silently dropped one branch: the same path added on two branches kept the id of the first parent only, and an ambiguous merge reset the id and with it the whole history of the file before the merge.

## 6. Tests

* `GitLogReaderTests` — the format, including unusual paths and truncated output.
* `FileIdentityTrackerTests` — the rules of section 2 and 3 on hand-built commits.
* `GitProviderTests` — the whole provider against small repositories created with the git command line at test time (`TestGitRepository`). They need `git` on the path and are skipped without it.
