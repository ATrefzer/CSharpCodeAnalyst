using System.Text.Json.Serialization;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public class PresentationState
{
    private Dictionary<string, bool> _nodeIdToCollapsed;

    /// <summary>
    /// Not persisted, tuples are not supported by System.Text.Json
    /// </summary>
    private Dictionary<(string, string), bool> _edgeToFlagged;

    private Dictionary<string, bool> _nodeIdToFlagged;

    public PresentationState(Dictionary<string, bool> initialState)
    {
        _nodeIdToCollapsed = initialState.ToDictionary(p => p.Key, p => p.Value);
        _nodeIdToFlagged = [];
        _edgeToFlagged = [];
        NodeIdToSearchHighlighted = [];
    }

    public PresentationState()
    {
        // Nothing is collapsed
        _nodeIdToCollapsed = [];
        _nodeIdToFlagged = [];
        _edgeToFlagged = [];
        NodeIdToSearchHighlighted = new Dictionary<string, bool>();
    }

    [JsonPropertyName("nodeIdToFlagged")] public Dictionary<string, bool> NodeIdToFlagged
    {
        get => _nodeIdToFlagged;
        set => _nodeIdToFlagged = value ?? [];
    }


    [JsonPropertyName("nodeIdToCollapsed")]
    public Dictionary<string, bool> NodeIdToCollapsed
    {
        get => _nodeIdToCollapsed;
        set => _nodeIdToCollapsed = value ?? [];
    }

    public Dictionary<string, bool> NodeIdToSearchHighlighted { get; }

    public PresentationState Clone()
    {
        var clone = new PresentationState();
        foreach (var pair in _nodeIdToCollapsed)
        {
            clone.SetCollapsedState(pair.Key, pair.Value);
        }

        foreach (var pair in _nodeIdToFlagged)
        {
            clone.SetFlaggedState(pair.Key, pair.Value);
        }

        foreach (var pair in _edgeToFlagged)
        {
            clone.SetFlaggedState(pair.Key, pair.Value);
        }

        foreach (var pair in NodeIdToSearchHighlighted)
        {
            clone.SetSearchHighlightedState(pair.Key, pair.Value);
        }

        return clone;
    }


    public bool IsCollapsed(string id)
    {
        _nodeIdToCollapsed.TryGetValue(id, out var isCollapsed);
        return isCollapsed;
    }

    public void SetCollapsedState(string id, bool isCollapsed)
    {
        _nodeIdToCollapsed[id] = isCollapsed;
    }

    public bool IsFlagged(string id)
    {
        _nodeIdToFlagged.TryGetValue(id, out var isFlagged);
        return isFlagged;
    }

    public bool IsFlagged((string, string) edge)
    {
        _edgeToFlagged.TryGetValue(edge, out var isFlagged);
        return isFlagged;
    }

    public void SetFlaggedState((string, string) edge, bool value)
    {
        _edgeToFlagged[edge] = value;
    }

    public void SetFlaggedState(string id, bool isFlagged)
    {
        _nodeIdToFlagged[id] = isFlagged;
    }

    public void ClearAllFlags()
    {
        _nodeIdToFlagged.Clear();
        _edgeToFlagged.Clear();
    }

    public bool IsSearchHighlighted(string id)
    {
        NodeIdToSearchHighlighted.TryGetValue(id, out var isSearchHighlighted);
        return isSearchHighlighted;
    }

    public void SetSearchHighlightedState(string id, bool isSearchHighlighted)
    {
        NodeIdToSearchHighlighted[id] = isSearchHighlighted;
    }

    public void ClearAllSearchHighlights()
    {
        NodeIdToSearchHighlighted.Clear();
    }

    internal void RemoveStates(HashSet<string> ids)
    {
        foreach (var id in ids)
        {
            _nodeIdToCollapsed.Remove(id);
            _nodeIdToFlagged.Remove(id);
            NodeIdToSearchHighlighted.Remove(id);
        }
    }
}