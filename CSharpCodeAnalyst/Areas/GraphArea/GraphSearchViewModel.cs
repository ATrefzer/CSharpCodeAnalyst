using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;
using Contracts.Graph;
using CSharpCodeAnalyst.Areas.SearchArea;
using CSharpCodeAnalyst.Common;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public class GraphSearchViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _searchTimer;
    private readonly IGraphViewer _graphViewer;
    
    private string _searchText;
    private bool _isSearchVisible;

    public GraphSearchViewModel(IGraphViewer graphViewer)
    {
        _graphViewer = graphViewer;
        _searchText = string.Empty;
        _isSearchVisible = false;

        // Subscribe to graph changes
        _graphViewer.GraphChanged += OnGraphChanged;

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

    private void OnGraphChanged(CodeGraph newGraph)
    {
        UpdateGraph(newGraph);
    }

    public void UpdateGraph(CodeGraph graph)
    {
        // Re-execute search with new graph if we have search text
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
        _graphViewer.ClearSearchHighlights();
    }

    private void ExecuteSearchInternal()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            _graphViewer.ClearSearchHighlights();
            return;
        }

        var root = SearchExpressionFactory.CreateSearchExpression(SearchText);
        var matchingNodeIds = new List<string>();

        var nodes = _graphViewer.GetGraph().Nodes;
        foreach (var node in nodes.Values)
        {
            if (root.Evaluate(node))
            {
                Debug.WriteLine($"Adding {nodes[node.Id]}");
                matchingNodeIds.Add(node.Id);
            }
        }

        _graphViewer.SetSearchHighlights(matchingNodeIds);
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}