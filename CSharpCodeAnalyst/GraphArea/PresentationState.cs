namespace CSharpCodeAnalyst.GraphArea;

public class PresentationState
{
    private readonly Dictionary<string, bool> _defaultState;
    private readonly Dictionary<string, bool> _nodeIdToCollapsed;

    public PresentationState(Dictionary<string, bool> defaultState)
    {
        _defaultState = defaultState.ToDictionary(p => p.Key, propa => propa.Value);
        _nodeIdToCollapsed = _defaultState.ToDictionary(p => p.Key, p => p.Value);
    }

    public PresentationState()
    {
        // Nothing is collapsed
        _defaultState = new Dictionary<string, bool>();
        _nodeIdToCollapsed = new Dictionary<string, bool>();
    }

    public PresentationState Clone()
    {
        var clone = new PresentationState(_defaultState);
        foreach (var pair in _nodeIdToCollapsed)
        {
            clone.SetCollapsedState(pair.Key, pair.Value);
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

    internal void RemoveStates(HashSet<string> ids)
    {
        foreach (var id in ids)
        {
            _nodeIdToCollapsed.Remove(id);
            _defaultState.Remove(id);
        }
    }
}