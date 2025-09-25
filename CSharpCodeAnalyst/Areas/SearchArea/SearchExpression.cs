namespace CSharpCodeAnalyst.Areas.SearchArea;

/// <summary>
///     Helper to build (very) simple search expressions with AND/OR/TERM
/// </summary>
internal interface IExpression
{
    bool Evaluate(SearchItemViewModel item);
}

internal class Term : IExpression
{
    private readonly bool _searchForType;

    private readonly string _searchTerm;

    public Term(string searchTerm)
    {
        if (searchTerm.StartsWith("type:"))
        {
            searchTerm = searchTerm.Substring("type:".Length);
            _searchForType = true;
        }

        _searchTerm = searchTerm;
    }

    public bool Evaluate(SearchItemViewModel item)
    {
        if (_searchForType)
        {
            return item.Type.Contains(_searchTerm, StringComparison.InvariantCultureIgnoreCase);
        }

        return item.FullPath.Contains(_searchTerm, StringComparison.InvariantCultureIgnoreCase);
    }
}

internal class And : IExpression
{
    private readonly IExpression[] _conditions;

    public And(params IExpression[] conditions)
    {
        _conditions = conditions;
    }

    public bool Evaluate(SearchItemViewModel item)
    {
        return _conditions.All(c => c.Evaluate(item));
    }
}

internal class Or : IExpression
{
    private readonly IExpression[] _conditions;

    public Or(params IExpression[] conditions)
    {
        _conditions = conditions;
    }

    public bool Evaluate(SearchItemViewModel item)
    {
        return _conditions.Any(c => c.Evaluate(item));
    }
}