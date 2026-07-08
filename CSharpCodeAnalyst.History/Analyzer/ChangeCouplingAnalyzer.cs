using System.Diagnostics;
using CSharpCodeAnalyst.History.Config;
using CSharpCodeAnalyst.History.Extensions;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Analyzer;

/// <summary>
///     Change coupling based on commits! Alternative would be logical work items.
/// </summary>
public sealed class ChangeCouplingAnalyzer
{
    private readonly Dictionary<string, uint> _count = new();
    private readonly Dictionary<string, Coupling> _couplings = new();

    public List<Coupling> CalculateChangeCouplings(ChangeSetHistory history)
    {
        _couplings.Clear();
        _count.Clear();

        var idToLocalFile = BuildIdToLocalFileMap(history);

        foreach (var cs in history.ChangeSets)
        {
            if (cs.Items.Count > Thresholds.MaxItemsInChangesetForChangeCoupling)
            {
                continue;
            }

            // Accepted files
            var itemIds = cs.Items.Select(i => i.Id).ToList(); //.Where(item => filter.IsAccepted(item.LocalPath)).Select(item => item.Id).ToList();

            // Do you have uncommitted changes.
            // Do you have commit items not inside the base directory?
            var missingFiles = itemIds.Select(id => idToLocalFile[id]).Where(file => !File.Exists(file));
            // Debug.Assert(!missingFiles.Any());

            IncrementCommitCount(itemIds);

            for (var i = 0; i < itemIds.Count - 1; i++) // Keep one for the last pair
            {
                // Make pairs of files.
                for (var j = i + 1; j < itemIds.Count; j++)
                {
                    var id1 = itemIds[i];
                    var id2 = itemIds[j];

                    IncrementCoupling(id1, id2);
                }
            }
        }

        CalculateDegree();

        return _couplings.Values
            .Where(coupling => coupling.Couplings >= Thresholds.MinCouplingForChangeCoupling && coupling.Degree >= Thresholds.MinDegreeForChangeCoupling)
            .OrderByDescending(coupling => coupling.Degree)
            .Select(c => new Coupling(idToLocalFile[c.Item1], idToLocalFile[c.Item2])
            {
                // Display coupling item with local path instead of identifier.
                Degree = c.Degree,
                Couplings = c.Couplings
            }).ToList();
    }

    private static Dictionary<string, string> BuildIdToLocalFileMap(ChangeSetHistory history)
    {
        var idToLocalFile = new Dictionary<string, string>();
        foreach (var cs in history.ChangeSets)
        {
            foreach (var item in cs.Items)
            {
                if (!idToLocalFile.ContainsKey(item.Id))
                {
                    // Seen the first time means latest file.
                    idToLocalFile.Add(item.Id, item.LocalPath);
                }
            }
        }

        return idToLocalFile;
    }

    /// <summary>
    ///     If the classifier returns string.EMPTY the according file is not used.
    /// </summary>
    public List<Coupling> CalculateClassifiedChangeCouplings(ChangeSetHistory history, Func<string, string> classifier)
    {
        _couplings.Clear();
        _count.Clear();

        foreach (var cs in history.ChangeSets)
        {
            var classifications = ClassifyItems(cs, classifier);
            IncrementCommitCount(classifications);

            for (var i = 0; i < classifications.Count - 1; i++) // Keep one for the last pair
            {
                // Make pairs of code classifiers.
                for (var j = i + 1; j < classifications.Count; j++)
                {
                    var class1 = classifications[i];
                    var class2 = classifications[j];

                    IncrementCoupling(class1, class2);
                }
            }
        }

        CalculateDegree();

        return _couplings.Values
            //  .Where(coupling => coupling.Couplings >= Thresholds.MinCouplingForChangeCoupling && coupling.Degree >= Thresholds.MinDegreeForChangeCoupling)
            .OrderByDescending(coupling => coupling.Degree).ToList();
    }

    private void CalculateDegree()
    {
        foreach (var coupling in _couplings.Values)
        {
            coupling.Degree = 100.0 * coupling.Couplings /
                              (GetCount(coupling.Item1) + GetCount(coupling.Item2) - coupling.Couplings);

            Debug.Assert(coupling.Degree <= 100);
            coupling.Degree = Math.Round(coupling.Degree, 2);
        }
    }

    private List<string> ClassifyItems(ChangeSet cs, Func<string, string> classifier)
    {
        // Get classifiers for changeset
        var set = new HashSet<string>();
        foreach (var item in cs.Items)
        {
            var classification = classifier(item.LocalPath);
            if (!string.IsNullOrEmpty(classification))
            {
                set.Add(classification);
            }
        }

        return set.ToList();
    }

    /// <summary>
    ///     Returns number of commits on this item
    /// </summary>
    private uint GetCount(string artifact)
    {
        uint value;
        _count.TryGetValue(artifact, out value);
        return value;
    }

    /// <summary>
    ///     File or classification. The given keys are from one change set. We increment each file (or class)
    ///     that occurs in the changeset.
    /// </summary>
    private void IncrementCommitCount(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            _count.AddToValue(key, 1);
        }
    }

    /// <summary>
    ///     File or classification. The given items occur together in a changeset.
    /// </summary>
    private void IncrementCoupling(string item1, string item2)
    {
        var pairKey = OrderedPair.Key(item1, item2);

        Coupling coupling;
        if (_couplings.TryGetValue(pairKey, out coupling))
        {
            coupling.Couplings = coupling.Couplings + 1;
        }
        else
        {
            _couplings.Add(pairKey, new Coupling(item1, item2));
        }
    }
}