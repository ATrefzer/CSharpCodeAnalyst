using CSharpCodeAnalyst.CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Symbol identification across compilations.
///     One of the main problems is that the symbols do not have a unique identifier across compilations.
///     For example a IMethodSymbol defined in one compilation may not be the same in another compilation implementing it.
///     Therefore, the Roslyn SymbolEqualityComparer is not useful for this application.
/// </summary>
public static class SymbolExtensions
{
    public static string BuildSymbolName(this ISymbol symbol)
    {
        var parts = GetSymbolChain(symbol);
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

        var parts = GetSymbolChain(symbol);
        return string.Join(".", parts.Select(GetKeyInternal));
    }


    /// <summary>
    ///     Sometimes when walking up the parent chain:
    ///     After the global namespace the containing symbol is not reliable.
    ///     If we do not end up at an assembly it is added manually.
    ///     0 = symbol itself
    ///     n = assembly
    /// </summary>
    public static List<ISymbol> GetSymbolChain(this ISymbol symbol)
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
            var compilation = globalNamespace.ContainingCompilation;
            if (compilation != null)
            {
                parts.Add(compilation.Assembly);
            }
        }

        return parts;
    }

    /// <summary>
    ///     Gets the source locations of a semantic symbol. We may have more than one location if
    ///     the symbol is defined over several files (i.e. partial classes)
    /// </summary>
    public static List<SourceLocation> GetSymbolLocations(this ISymbol symbol)
    {
        return symbol.Locations.Select(l => new SourceLocation
        {
            File = l.SourceTree?.FilePath ?? "",
            Line = l.GetLineSpan().StartLinePosition.Line + 1,
            Column = l.GetLineSpan().StartLinePosition.Character + 1
        }).ToList();
    }

    private static string GetKeyInternal(ISymbol symbol)
    {
        var name = symbol.Name;
        var genericPart = GetGenericPart(symbol);
        var kind = symbol.Kind.ToString();

        if (symbol is IAssemblySymbol)
        {
            // Yes, people exist who add two projects with the same name in one solution
            // Ignore this for the moment
            return $"{name}";
        }

        if (symbol is IMethodSymbol methodSymbol)
        {
            var parameters = string.Join("_", methodSymbol.Parameters.Select(GetParameterKey));
            return $"{name}{genericPart}_{parameters}_{kind}";
        }

        // Indexers are overloadable on their parameter list (this[int] vs this[string]); without the
        // parameters both overloads share a key and one element is silently dropped in phase 1.
        if (symbol is IPropertySymbol { IsIndexer: true } indexerSymbol)
        {
            var parameters = string.Join("_", indexerSymbol.Parameters.Select(GetParameterKey));
            return $"{name}{genericPart}_{parameters}_{kind}";
        }

        return $"{name}{genericPart}_{kind}";
    }

    /// <summary>
    ///     Encodes a parameter for the symbol key. The ref-kind (ref/out/in/ref readonly) is part of the
    ///     signature: M(int) and M(ref int) are legal overloads and must not collapse to the same key.
    /// </summary>
    private static string GetParameterKey(IParameterSymbol parameter)
    {
        var refKind = parameter.RefKind == RefKind.None ? string.Empty : parameter.RefKind + " ";
        return $"{refKind}{parameter.Type.ToDisplayString()}";
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

    public static bool IsFromSource(this ISymbol symbol)
    {
        return symbol.Locations.Any(loc => loc.IsInSource);
    }

    /// <summary>
    ///     A partial method has two symbols (definition part and implementation part) that share one
    ///     code element (same <see cref="Key" />); phase 1 stores whichever it saw first. Walking only
    ///     that symbol's declarations would lose the body of the other part - declaration order in the
    ///     source decides which one, so dependencies would silently depend on file layout. Returns the
    ///     declaring syntax references of both parts.
    /// </summary>
    public static IEnumerable<SyntaxReference> GetDeclaringSyntaxReferencesIncludingPartial(this IMethodSymbol method)
    {
        var otherPart = (ISymbol?)method.PartialImplementationPart ?? method.PartialDefinitionPart;
        return otherPart is null
            ? method.DeclaringSyntaxReferences
            : method.DeclaringSyntaxReferences.Concat(otherPart.DeclaringSyntaxReferences);
    }

    /// <summary>
    ///     <inheritdoc cref="GetDeclaringSyntaxReferencesIncludingPartial(IMethodSymbol)" />
    ///     Same for partial properties (C# 13).
    /// </summary>
    public static IEnumerable<SyntaxReference> GetDeclaringSyntaxReferencesIncludingPartial(this IPropertySymbol property)
    {
        var otherPart = (ISymbol?)property.PartialImplementationPart ?? property.PartialDefinitionPart;
        return otherPart is null
            ? property.DeclaringSyntaxReferences
            : property.DeclaringSyntaxReferences.Concat(otherPart.DeclaringSyntaxReferences);
    }

    /// <summary>
    ///     Returns true if the ctor is explicit and has a body. False if  implicit or primary ctor.
    /// </summary>
    public static bool IsExplicitConstructor(this IMethodSymbol ctor)
    {
        if (ctor.MethodKind != MethodKind.Constructor)
        {
            return false;
        }

        // Implicit parameterless ctor
        if (ctor.IsImplicitlyDeclared)
        {
            return false;
        }

        // Normal ctor → ConstructorDeclarationSyntax.
        // Primary ctor → TypeDeclarationSyntax (class, record, struct).
        var isPrimary = ctor.DeclaringSyntaxReferences
            .Any(r => r.GetSyntax() is TypeDeclarationSyntax);

        return !isPrimary;
    }

    /// <summary>
    ///     Returns the original definition of a symbol.
    /// 
    ///     Note:
    ///     Constructors are never generic. So IsGeneric is never true. But phase 1 in our parser did not collect
    ///     constructed types.
    /// 
    ///     Examples:
    ///     - List&lt;int&gt;.Add -> List&lt;T&gt;.Add
    ///     - Dictionary&lt;string, int&gt; -> Dictionary&lt;TKey, TValue&gt;
    /// </summary>
    public static ISymbol NormalizeToOriginalDefinition(this ISymbol symbol)
    {
        return symbol switch
        {
            // Constructors are never generic in C#. We use the symbol of the definition found in phase 1
            // So IsGeneric is never true, yet we need the original definition.
            // TestCase: GenericUtilities.GenericPair in TestSuite.
            IMethodSymbol { MethodKind: MethodKind.Constructor } ctor => ctor.OriginalDefinition,

            // Generic method (independent of container)
            IMethodSymbol { IsGenericMethod: true, IsDefinition: false } method => method.OriginalDefinition,

            // Member in generic container
            IMethodSymbol { ContainingType: { IsGenericType: true, IsDefinition: false } } method => method.OriginalDefinition,
            IPropertySymbol { ContainingType: { IsGenericType: true, IsDefinition: false } } property => property.OriginalDefinition,
            IFieldSymbol { ContainingType: { IsGenericType: true, IsDefinition: false } } field => field.OriginalDefinition,
            IEventSymbol { ContainingType: { IsGenericType: true, IsDefinition: false } } @event => @event.OriginalDefinition,

            // Generic type
            INamedTypeSymbol { IsGenericType: true, IsDefinition: false } type => type.OriginalDefinition,

            _ => symbol
        };
    }

    /// <summary>
    ///     Generic overload that preserves the specific symbol type.
    ///     Eliminates the need for casts at call sites.
    /// </summary>
    public static T NormalizeToOriginalDefinition<T>(this T symbol) where T : ISymbol
    {
        return (T)NormalizeToOriginalDefinition((ISymbol)symbol);
    }
}