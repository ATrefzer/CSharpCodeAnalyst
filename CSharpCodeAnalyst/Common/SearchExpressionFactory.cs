namespace CSharpCodeAnalyst.Common;

internal static class SearchExpressionFactory
{
    private static Term CreateTerm(string search, TextSearchField searchField)
    {
        if (searchField == TextSearchField.FullName)
        {
            return new FullNameSearch(search);
        }

        return new NameSearch(search);
    }

    public static IExpression CreateSearchExpression(string searchText, TextSearchField searchField = TextSearchField.FullName)
    {
        // Or binds less.
        var orTerms = searchText
            .Split(['|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var orExpressions = new List<IExpression>();
        foreach (var orTerm in orTerms)
        {
            var andExpressions = orTerm
                .Split([' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(IExpression (t) => CreateTerm(t, searchField))
                .ToArray();

            orExpressions.Add(new Term.And(andExpressions));
        }

        if (orExpressions.Count == 1)
        {
            return orExpressions[0];
        }

        var root = new Term.Or(orExpressions.ToArray());
        return root;
    }

    internal enum TextSearchField
    {
        FullName,
        Name
    }
}