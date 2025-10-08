using System.Text.RegularExpressions;
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
    private readonly SearchModel _searchModel;

    private readonly string _searchTerm;
    private readonly CodeElementType _type = CodeElementType.Other;
    
    private readonly Regex? _regex;

    public Term(string searchTerm)
    {
        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        if (lowerSearchTerm.StartsWith("type:"))
        {
            // If type is not known fallback to CodeElementType.Other
            lowerSearchTerm = lowerSearchTerm.Substring("type:".Length);
            if (TryGetCodeElementTypeFromName(lowerSearchTerm, out _type))
            {
                _searchModel = SearchModel.Type;
            }
        }
        else if (lowerSearchTerm is "source:intern")
        {
            _searchModel = SearchModel.InternalCode;
        }
        else if (lowerSearchTerm is "source:extern")
        {
            _searchModel = SearchModel.ExternalCode;
        }
        else
        {
            var (isPascalCase, regex) = PascalCaseSearch.CreateSearchRegex(searchTerm);
            if (isPascalCase && regex != null)
            {
                _searchModel = SearchModel.FullNameResharperStyle;
                _regex = regex; 
            }
            else
            {
                // All lower case, default mode
                _searchModel = SearchModel.FullNameSimple;
                _searchTerm = lowerSearchTerm;        
            }
        }
    }

    public bool Evaluate(CodeElement item)
    {
        return _searchModel switch
        {
            SearchModel.Type => item.ElementType == _type,
            SearchModel.InternalCode => !item.IsExternal,
            SearchModel.ExternalCode => item.IsExternal,
            SearchModel.FullNameResharperStyle => _regex.IsMatch(item.FullName),
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

    private enum SearchModel
    {
        // Search for types.
        Type,
        
        // Search in FullName
        FullNameSimple,
        
        FullNameResharperStyle,
        ExternalCode,
        InternalCode,
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