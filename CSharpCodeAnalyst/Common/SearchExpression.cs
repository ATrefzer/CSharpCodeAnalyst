using Contracts.Graph;

namespace CSharpCodeAnalyst.Common;

/// <summary>
///     Helper to build (very) simple search expressions with AND/OR/TERM
/// </summary>
internal interface IExpression
{
    bool Evaluate(CodeElement item);
}

internal class Term : IExpression
{
    private enum SearchLocation
    {
        Type,
        Name,
        External,
        Internal
    }
    private readonly SearchLocation _searchLocation;

    private readonly string _searchTerm;
    private readonly CodeElementType _type = CodeElementType.Other;

    public Term(string searchTerm)
    {
        if (searchTerm.StartsWith("type:"))
        {
            searchTerm = searchTerm.Substring("type:".Length);
            if (TryGetCodeElementTypeFromName(searchTerm, out _type))
            {
                _searchLocation = SearchLocation.Type;
            }
        }
        else if (searchTerm is "internal" or "intern")
        {
            _searchLocation = SearchLocation.Internal;
        }
        else if (searchTerm is "external" or "extern")
        {
            _searchLocation = SearchLocation.External;
        }

        _searchLocation = SearchLocation.Name;
        _searchTerm = searchTerm;
    }

    public bool Evaluate(CodeElement item)
    {
        return _searchLocation switch
        {
            SearchLocation.Type => item.ElementType == _type,
            SearchLocation.Internal => !item.IsExternal,
            SearchLocation.External => item.IsExternal,
            _ => item.FullName.Contains(_searchTerm, StringComparison.InvariantCultureIgnoreCase)
        };
    }

    private static bool TryGetCodeElementTypeFromName(string typeName, out CodeElementType type)
    {
        typeName = typeName.ToLowerInvariant();
        var codeElements = Enum.GetValues<CodeElementType>();

        foreach (var codeElement in codeElements)
        {
            if (codeElement.ToString().ToLowerInvariant() != typeName)
            {
                continue;
            }

            type = codeElement;
            return true;
        }

        type = CodeElementType.Other;
        return false;
    }

    internal class And : IExpression
    {
        private readonly IExpression[] _conditions;

        public And(params IExpression[] conditions)
        {
            _conditions = conditions;
        }

        public bool Evaluate(CodeElement item)
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

        public bool Evaluate(CodeElement item)
        {
            return _conditions.Any(c => c.Evaluate(item));
        }
    }
}