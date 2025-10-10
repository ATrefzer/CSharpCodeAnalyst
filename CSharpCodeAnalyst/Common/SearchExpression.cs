using System.Text.RegularExpressions;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Common;

/// <summary>
///     Helper to build (very) simple search expressions with AND/OR/TERM
/// </summary>
internal interface IExpression
{
    bool Evaluate(CodeElement? item);
}

internal abstract class Term : IExpression
{
    protected readonly Regex? Regex;
    protected readonly SearchType SearchMode;
    protected readonly string SearchTerm = string.Empty;
    protected readonly CodeElementType Type = CodeElementType.Other;

    protected Term(string searchTerm)
    {
        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        if (lowerSearchTerm.StartsWith("type:"))
        {
            // If type is not known fallback to CodeElementType.Other
            lowerSearchTerm = lowerSearchTerm.Substring("type:".Length);
            if (TryGetCodeElementTypeFromName(lowerSearchTerm, out Type))
            {
                SearchMode = SearchType.Type;
            }
        }
        else if (lowerSearchTerm is "source:intern")
        {
            SearchMode = SearchType.InternalCode;
        }
        else if (lowerSearchTerm is "source:extern")
        {
            SearchMode = SearchType.ExternalCode;
        }
        else
        {
            var (isPascalCase, regex) = PascalCaseSearch.CreateSearchRegex(searchTerm);
            if (isPascalCase && regex != null)
            {
                SearchMode = SearchType.FullNameResharperStyle;
                Regex = regex;
            }
            else
            {
                // All lower case, default mode
                SearchMode = SearchType.FullNameSimple;
                SearchTerm = lowerSearchTerm;
            }
        }
    }

    public abstract bool Evaluate(CodeElement item);



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

    internal enum SearchType
    {
        // Search for types.
        Type,

        // Search in FullName
        FullNameSimple,

        FullNameResharperStyle,
        ExternalCode,
        InternalCode
    }

    internal class And : IExpression
    {
        private readonly IExpression[] _conditions;

        public And(params IExpression[] conditions)
        {
            _conditions = conditions;
        }

        public bool Evaluate(CodeElement? item)
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

        public bool Evaluate(CodeElement? item)
        {
            return _conditions.Any(c => c.Evaluate(item));
        }
    }
}

internal class FullNameSearch(string searchTerm) : Term(searchTerm)
{
    public override bool Evaluate(CodeElement? item)
    {
        if (item == null)
        {
            return false;
        }

        return SearchMode switch
        {
            SearchType.Type => item.ElementType == Type,
            SearchType.InternalCode => !item.IsExternal,
            SearchType.ExternalCode => item.IsExternal,
            SearchType.FullNameResharperStyle => Regex!.IsMatch(item.FullName),
            _ => item.FullName.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase)
        };
    }
}

internal class NameSearch(string searchTerm) : Term(searchTerm)
{
    public override bool Evaluate(CodeElement? item)
    {
        if (item == null)
        {
            return false;
        }

        return SearchMode switch
        {
            SearchType.Type => item.ElementType == Type,
            SearchType.InternalCode => !item.IsExternal,
            SearchType.ExternalCode => item.IsExternal,
            SearchType.FullNameResharperStyle => Regex!.IsMatch(item.Name),
            _ => item.Name.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase)
        };
    }
}