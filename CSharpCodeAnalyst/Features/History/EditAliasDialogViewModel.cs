using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace CSharpCodeAnalyst.Features.History;

/// <summary>
///     Backing model for the <see cref="EditAliasDialog" />. Presents one row per developer and
///     collects the alias the user assigns to each. Developers sharing an alias are later treated
///     as one entity (e.g. a team) by the history analyses.
/// </summary>
public sealed class EditAliasDialogViewModel
{
    private string _searchText = string.Empty;

    public EditAliasDialogViewModel(IEnumerable<string> developers, IReadOnlyDictionary<string, string> currentMapping)
    {
        var rows = developers
            .OrderBy(developer => developer, StringComparer.OrdinalIgnoreCase)
            .Select(developer => new AliasRow(developer, currentMapping.TryGetValue(developer, out var alias) ? alias : developer))
            .ToList();

        Rows = new ObservableCollection<AliasRow>(rows);

        foreach (var row in Rows)
        {
            // The alias column commits on cell leave (UpdateSourceTrigger=LostFocus), so refreshing
            // the suggestion list on every change keeps freshly typed team names available for the
            // next developer without churning the drop-down on every keystroke.
            row.PropertyChanged += OnRowChanged;
        }

        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter = FilterRow;

        RefreshAliasSuggestions();
    }

    public ObservableCollection<AliasRow> Rows { get; }

    /// <summary>Filtered / ordered view over <see cref="Rows" /> bound by the data grid.</summary>
    public ICollectionView RowsView { get; }

    /// <summary>Simple substring search over developer and alias; empty shows everything.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value ?? string.Empty;
            RowsView.Refresh();
        }
    }

    /// <summary>Already assigned aliases, offered as suggestions in the editable alias combo box.</summary>
    public ObservableCollection<string> AliasSuggestions { get; } = new();

    private bool FilterRow(object item)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return true;
        }

        var row = (AliasRow)item;
        return row.Developer.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
               || row.Alias.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resets every developer's alias back to the default: his own name.</summary>
    public void ResetToDefaults()
    {
        foreach (var row in Rows)
        {
            row.Alias = row.Developer;
        }
    }

    /// <summary>
    ///     Returns the final mapping after the dialog was closed
    /// </summary>
    public Dictionary<string, string> GetMapping()
    {
        var mapping = new Dictionary<string, string>();
        foreach (var row in Rows)
        {
            var alias = string.IsNullOrWhiteSpace(row.Alias) ? row.Developer : row.Alias.Trim();
            mapping[row.Developer] = alias;
        }

        return mapping;
    }


    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AliasRow.Alias))
        {
            RefreshAliasSuggestions();
        }
    }

    private void RefreshAliasSuggestions()
    {
        // Aliases the user actually assigned (they differ from the developer's own name) are the
        // useful team names to reuse, so they head the drop-down. The plain self-name defaults are
        // just noise and go to the bottom. Each group stays alphabetical.
        var custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows)
        {
            var alias = row.Alias?.Trim();
            if (!string.IsNullOrEmpty(alias) && !string.Equals(alias, row.Developer, StringComparison.Ordinal))
            {
                custom.Add(alias);
            }
        }

        var ordered = Rows
            .Select(row => row.Alias?.Trim())
            .Where(alias => !string.IsNullOrEmpty(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(alias => custom.Contains(alias!))
            .ThenBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AliasSuggestions.Clear();
        foreach (var alias in ordered)
        {
            AliasSuggestions.Add(alias!);
        }
    }
}

public sealed class AliasRow : INotifyPropertyChanged
{
    private string _alias;

    public AliasRow(string developer, string alias)
    {
        Developer = developer;
        _alias = alias;
    }

    public string Developer { get; }

    public string Alias
    {
        get => _alias;
        set
        {
            if (_alias != value)
            {
                _alias = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}