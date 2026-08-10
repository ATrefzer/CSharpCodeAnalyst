namespace CSharpCodeAnalyst.CodeGraph.Search;

public static class SearchExpressionFactory
{
    /// <summary>
    ///     Marks a term as excluding rather than selecting. A minus sign cannot start a C# identifier, so
    ///     it is free to use here; imported graphs may contain one inside a name, but not at the start of
    ///     a search term.
    /// </summary>
    private const char NegationPrefix = '-';

    private static Term CreateTerm(string search, TextSearchField searchField)
    {
        if (searchField == TextSearchField.FullName)
        {
            return new FullNameSearch(search);
        }

        return new NameSearch(search);
    }

    /// <summary>
    ///     Wraps the term in a negation when it starts with '-'. The negation belongs to its own term, so
    ///     it binds tighter than the AND of a group and than the OR between groups: "-a b | c" reads as
    ///     "((NOT a) AND b) OR c". A lone '-' has nothing to negate and stays a literal search term.
    /// </summary>
    private static IExpression CreateTermOrNegation(string token, TextSearchField searchField, bool allowNegation)
    {
        if (allowNegation && token.Length > 1 && token[0] == NegationPrefix)
        {
            return new Term.Not(CreateTerm(token[1..], searchField));
        }

        return CreateTerm(token, searchField);
    }

    /// <param name="allowNegation">
    ///     Whether a leading '-' excludes the term. Pass false where an expression that matches almost
    ///     everything is harmful rather than useful: the tree expands and highlights every ancestor of a
    ///     match, so an exclusion would unfold the whole tree at once. With negation off the '-' is part
    ///     of the search term like any other character.
    /// </param>
    public static IExpression CreateSearchExpression(string searchText,
        TextSearchField searchField = TextSearchField.FullName, bool allowNegation = true)
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
                .Select(t => CreateTermOrNegation(t, searchField, allowNegation))
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

    public enum TextSearchField
    {
        FullName,
        Name
    }
}