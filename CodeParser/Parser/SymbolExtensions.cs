using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace CodeParser.Parser;

public static class SymbolExtensions
{
    /// <summary>
    ///     Sometimes when walking up the parent chain:
    ///     After the global namespace the containing symbol is not reliable.
    ///     If we do not end up at an assembly it is added manually.
    /// </summary>
    public static string BuildSymbolName(this ISymbol symbol)
    {
        var parts = new List<string>();

        var currentSymbol = symbol;
        ISymbol? lastKnownSymbol = null;

        while (currentSymbol != null)
        {
            if (currentSymbol is IModuleSymbol)
            {
                // Skip the module symbol
                currentSymbol = currentSymbol.ContainingSymbol;
            }

            lastKnownSymbol = currentSymbol;

            var name = currentSymbol.Name;
            parts.Add(name);
            currentSymbol = currentSymbol.ContainingSymbol;
        }

        if (lastKnownSymbol is not IAssemblySymbol)
        {
            // global namespace has the ContainingCompilation set.
            Debug.Assert(lastKnownSymbol is INamespaceSymbol { IsGlobalNamespace: true });
            var namespaceSymbol = lastKnownSymbol as INamespaceSymbol;
            var assemblySymbol = namespaceSymbol.ContainingCompilation.Assembly;
            parts.Add(assemblySymbol.Name);
        }

        parts.Reverse();
        var fullName = string.Join(".", parts.Where(p => !string.IsNullOrEmpty(p)));
        return fullName;
    }

    /// <summary>
    /// Returns a unique key for the symbol
    /// We may have for example multiple symbols for the same namespace (X.Y.Z vs X.Y{Z})
    /// See INamespaceSymbol.ConstituentNamespaces
    /// Method overloads and generics are considered in the key.
    /// This key more or less replaces the SymbolEqualityComparer not useful for this application.
    /// </summary>
    public static string Key(this ISymbol symbol)
    {
        var fullName = BuildSymbolName(symbol);
        var genericPart = GetGenericPart(symbol);
        var kind = symbol.Kind.ToString();

        if (symbol is IMethodSymbol methodSymbol)
        {
            var parameters = string.Join("_", methodSymbol.Parameters.Select(p => p.Type.ToDisplayString()));
            return $"{fullName}{genericPart}_{parameters}_{kind}";
        }

        return $"{fullName}{genericPart}_{kind}";
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
                        result = $"<{string.Join(",", namedTypeSymbol.TypeArguments.Select(t => t.ToDisplayString()))}>";
                    }
                    else
                    {
                        // This is a generic type definition (e.g., List<T>)
                        // When processing the solution hierarchy or dependency to original definition symbol
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