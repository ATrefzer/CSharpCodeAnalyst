using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using CSharpCodeAnalyst.Shared.Search;
using CSharpCodeAnalyst.Shared.Wpf;

namespace CSharpCodeAnalyst.Features.Graph;

public sealed class GraphSearchViewModel : INotifyPropertyChanged
{
    private readonly GraphViewState _state;
    private readonly DispatcherTimer _searchTimer;



    private bool _isSearchVisible;

    private string _searchText;

    public GraphSearchViewModel(GraphViewState state)
    {
        _state = state;
        _searchText = string.Empty;
        _isSearchVisible = false;
        ClearSearchCommand = new WpfCommand(ClearSearch);

        // Re-run the search when the graph changes (render-agnostic; shared by both views).
        _state.Changed += OnStateChanged;

        // Initialize debounce timer for search
        _searchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300) // 300ms debounce
        };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ExecuteSearchInternal();
        };
    }



    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                // Execute search with debouncing
                _searchTimer.Stop();
                _searchTimer.Start();
            }
        }
    }

    public ICommand ClearSearchCommand { get; }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set
        {
            if (_isSearchVisible != value)
            {
                _isSearchVisible = value;
                OnPropertyChanged(nameof(IsSearchVisible));

                // Clear search when hiding
                if (!value)
                {
                    ClearSearch();
                }
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnStateChanged()
    {
        // Re-execute search against the new graph if we have search text.
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            ExecuteSearchInternal();
        }
    }

    public void ToggleSearchVisibility()
    {
        IsSearchVisible = !IsSearchVisible;
    }

    public void ClearSearch()
    {
        SearchText = string.Empty;
        _state.ClearSearchHighlights();
    }

    private void ExecuteSearchInternal()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            _state.ClearSearchHighlights();
            return;
        }

        var root = SearchExpressionFactory.CreateSearchExpression(SearchText);
        var matchingNodeIds = new List<string>();

        var nodes = _state.CodeGraph.Nodes;
        foreach (var node in nodes.Values)
        {
            if (root.Evaluate(node))
            {
                matchingNodeIds.Add(node.Id);
            }
        }

        _state.SetSearchHighlights(matchingNodeIds);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}