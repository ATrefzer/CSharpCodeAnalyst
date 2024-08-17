namespace CSharpCodeAnalyst.TreeArea;

internal class Matcher
{
    private string _expression = string.Empty;

    public bool AcceptsAll => string.IsNullOrEmpty(_expression);

    public void LoadMatchExpression(string? expression)
    {
        expression ??= string.Empty;
        _expression = expression.Trim();
    }

    public bool IsMatch(TreeItemViewModel item)
    {
        if (string.IsNullOrEmpty(_expression))
        {
            return true;
        }

        if (item.Name is null || item.Type is null)
        {
            return false;
        }

        var matchesFilter =
            item.Name.Contains(_expression, StringComparison.OrdinalIgnoreCase) ||
            item.Type.Contains(_expression, StringComparison.OrdinalIgnoreCase);

        return matchesFilter;
    }
}