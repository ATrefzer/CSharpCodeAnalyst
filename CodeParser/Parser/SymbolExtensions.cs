using Microsoft.CodeAnalysis;

namespace CodeParser.Parser;

/// <summary>
///     Symbol identification across compilations.
///     One of the main problems is that the symbols do not have a unique identifier.
///     For example a IMethodSymbol defined in one compilation may not the same as a IMethodSymbol
///     used in an invocation in another compilation.
/// </summary>
public static class SymbolExtensions
{
    public static string BuildSymbolName(this ISymbol symbol)
    {
        var parts = GetParentChain(symbol);
        parts.Reverse();
        var fullName = string.Join(".", parts.Where(p => !string.IsNullOrEmpty(p.Name)).Select(p => p.Name));
        return fullName;
    }

    /// <summary>
    ///     Returns a unique key for the symbol
    ///     We may have for example multiple symbols for the same namespace (X.Y.Z vs X.Y{Z})
    ///     See INamespaceSymbol.ConstituentNamespaces
    ///     Method overloads and generics are considered in the key.
    ///     This key more or less replaces the SymbolEqualityComparer not useful for this application.
    /// </summary>
    public static string Key(this ISymbol symbol)
    {
        // A generic method may be in a generic type. So we have to consider the generic part of the parent, too

        var parts = GetParentChain(symbol);
        return string.Join(".", parts.Select(GetKeyInternal));
    }


    /// <summary>
    /// Returns a key for the symbol only without the parent chain.
    /// This key can identify the overrides of a symbol in a class hierarchy
    /// </summary>
    public static string KeySymbolOnly(this ISymbol symbol)
    {
        return GetKeyInternal(symbol);
    }


    /// <summary>
    ///     Sometimes when walking up the parent chain:
    ///     After the global namespace the containing symbol is not reliable.
    ///     If we do not end up at an assembly it is added manually.
    ///     0 = symbol itself
    ///     n = assembly
    /// </summary>
    private static List<ISymbol> GetParentChain(this ISymbol symbol)
    {
        var parts = new List<ISymbol>();

        while (symbol != null)
        {
            if (symbol is IModuleSymbol)
            {
                symbol = symbol.ContainingSymbol;
                continue; // Skip the module symbol
            }

            parts.Add(symbol);
            symbol = symbol.ContainingSymbol;
        }

        // Check if the last symbol is a global namespace and add the assembly
        if (parts.LastOrDefault() is INamespaceSymbol { IsGlobalNamespace: true } globalNamespace)
        {
            parts.Add(globalNamespace.ContainingCompilation.Assembly);
        }

        return parts;
    }

  
    private static string GetKeyInternal(ISymbol symbol)
    {
        var name = symbol.Name;
        var genericPart = GetGenericPart(symbol);
        var kind = symbol.Kind.ToString();

        if (symbol is IAssemblySymbol assemblySymbol)
        {
            // Yes, people exist who add two projects with the same name in one solution
            // Ignore this for the moment
            return $"{name}";
        }

        if (symbol is IMethodSymbol methodSymbol)
        {
            var parameters = string.Join("_", methodSymbol.Parameters.Select(p => p.Type.ToDisplayString()));
            return $"{name}{genericPart}_{parameters}_{kind}";
        }

        return $"{name}{genericPart}_{kind}";
    }

    private static string GetGenericPart(ISymbol symbol)
    {
        var result = string.Empty;
        switch (symbol)
        {
            case INamedTypeSymbol namedTypeSymbol:
                if (namedTypeSymbol.IsGenericType)
                {
                    if (!namedTypeSymbol.IsDefinition)
                    {
                        // This is a constructed type (e.g., List<int>)
                        result =
                            $"<{string.Join(",", namedTypeSymbol.TypeArguments.Select(t => t.ToDisplayString()))}>";
                    }
                    else
                    {
                        // This is a generic type definition (e.g., List<T>)
                        // When processing the solution hierarchy or relationship to original definition symbol
                        result = $"<{string.Join(",", namedTypeSymbol.TypeParameters.Select(t => t.Name))}>";
                    }
                }

                break;

            case IMethodSymbol methodSymbol:
                if (methodSymbol.IsGenericMethod)
                {
                    if (!methodSymbol.IsDefinition)
                    {
                        // This is a constructed generic method
                        result = $"<{string.Join(",", methodSymbol.TypeArguments.Select(t => t.ToDisplayString()))}>";
                    }
                    else
                    {
                        // This is a generic method definition
                        result = $"<{string.Join(",", methodSymbol.TypeParameters.Select(t => t.Name))}>";
                    }
                }

                break;
        }

        return result;
    }
}