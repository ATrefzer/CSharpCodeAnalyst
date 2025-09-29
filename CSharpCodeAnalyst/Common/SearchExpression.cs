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
    private readonly bool _searchForType;

    private readonly string _searchTerm;
    private readonly CodeElementType _type = CodeElementType.Other;

    public Term(string searchTerm)
    {
        if (searchTerm.StartsWith("type:"))
        {
            searchTerm = searchTerm.Substring("type:".Length);
            if (TryGetCodeElementTypeFromName(searchTerm, out _type))
            {
                _searchForType = true;
            }
        }

        _searchTerm = searchTerm;
    }

    public bool Evaluate(CodeElement item)
    {
        if (_searchForType)
        {
            return item.ElementType == _type;
        }

        return item.FullName.Contains(_searchTerm, StringComparison.InvariantCultureIgnoreCase);
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