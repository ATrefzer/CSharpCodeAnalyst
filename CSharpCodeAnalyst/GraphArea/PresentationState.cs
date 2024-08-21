namespace CSharpCodeAnalyst.GraphArea;

class PresentationState
{
    public PresentationState(Dictionary<string, bool> defaultState)
    {
        _defaultState = defaultState.ToDictionary(p => p.Key, propa => propa.Value);
        RestoreDefault();
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

    public PresentationState()
    {
        // Nothing is collapsed
        _defaultState = new();
        _nodeIdToCollapsed = new();
    }

    private readonly Dictionary<string, bool> _defaultState;
    private Dictionary<string, bool> _nodeIdToCollapsed;


    public bool IsCollapsed(string id)
    {
        _nodeIdToCollapsed.TryGetValue(id, out var isCollapsed);
        return isCollapsed;
    }

    public void RestoreDefault()
    {
        _nodeIdToCollapsed = _defaultState.ToDictionary(p => p.Key, p => p.Value);
    }

    public void SetCollapsedState(string id, bool isCollapsed)
    {
        _nodeIdToCollapsed[id] = isCollapsed;
    }
}