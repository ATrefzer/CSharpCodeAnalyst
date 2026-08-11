using System.Text.RegularExpressions;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Search;

/// <summary>
///     Helper to build (very) simple search expressions with AND/OR/TERM
/// </summary>
public interface IExpression
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
        else if (lowerSearchTerm is "source:generated")
        {
            // Code a tool wrote. Mostly useful negated ("-source:generated"), because a resx designer or
            // the XAML markup compiler contributes rows nobody can act on.
            SearchMode = SearchType.GeneratedCode;
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

    public abstract bool Evaluate(CodeElement? item);


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
        InternalCode,

        // Written by a tool rather than a person (CodeElement.IsGenerated).
        GeneratedCode
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

    /// <summary>
    ///     Negates a condition, so a search can exclude instead of select ("-Strings." hides everything
    ///     whose name contains "Strings.").
    ///     <para>
    ///         An item without a code element never matches, not even a negated condition. Every term
    ///         answers "no" for a null item, and negation must not silently turn that into a match - the
    ///         tree has a virtual root without a code element that would otherwise light up on every
    ///         exclusion.
    ///     </para>
    /// </summary>
    internal class Not : IExpression
    {
        private readonly IExpression _condition;

        public Not(IExpression condition)
        {
            _condition = condition;
        }

        public bool Evaluate(CodeElement? item)
        {
            return item is not null && !_condition.Evaluate(item);
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
            SearchType.GeneratedCode => item.IsGenerated,
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
            SearchType.GeneratedCode => item.IsGenerated,
            SearchType.FullNameResharperStyle => Regex!.IsMatch(item.Name),
            _ => item.Name.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase)
        };
    }
}