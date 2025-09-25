using System.Text.Json.Serialization;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public class PresentationState
{
    private readonly Dictionary<string, bool> _nodeIdToCollapsed;
    private Dictionary<string, bool> _defaultState;
    private Dictionary<string, bool> _nodeIdToFlagged;

    public PresentationState(Dictionary<string, bool> defaultState)
    {
        _defaultState = defaultState.ToDictionary(p => p.Key, p => p.Value);
        _nodeIdToCollapsed = _defaultState.ToDictionary(p => p.Key, p => p.Value);
        _nodeIdToFlagged = new Dictionary<string, bool>();
    }

    public PresentationState()
    {
        // Nothing is collapsed
        _defaultState = new Dictionary<string, bool>();
        _nodeIdToCollapsed = new Dictionary<string, bool>();
        _nodeIdToFlagged = new Dictionary<string, bool>();
    }

    // Public properties for JSON serialization
    [JsonPropertyName("defaultState")] public Dictionary<string, bool> DefaultState
    {
        get => _defaultState;
        set => _defaultState = value ?? new Dictionary<string, bool>();
    }

    [JsonPropertyName("nodeIdToFlagged")] public Dictionary<string, bool> NodeIdToFlagged
    {
        get => _nodeIdToFlagged;
        set => _nodeIdToFlagged = value ?? new Dictionary<string, bool>();
    }

    public PresentationState Clone()
    {
        var clone = new PresentationState(_defaultState);
        foreach (var pair in _nodeIdToCollapsed)
        {
            clone.SetCollapsedState(pair.Key, pair.Value);
        }

        foreach (var pair in _nodeIdToFlagged)
        {
            clone.SetFlaggedState(pair.Key, pair.Value);
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

    public void SetFlaggedState(string id, bool isFlagged)
    {
        _nodeIdToFlagged[id] = isFlagged;
    }

    public void ClearAllFlags()
    {
        _nodeIdToFlagged.Clear();
    }

    internal void RemoveStates(HashSet<string> ids)
    {
        foreach (var id in ids)
        {
            _nodeIdToCollapsed.Remove(id);
            _nodeIdToFlagged.Remove(id);
            _defaultState.Remove(id);
        }
    }
}