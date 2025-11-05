using System.Diagnostics;
using CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Symbol identification across compilations.
///     One of the main problems is that the symbols do not have a unique identifier across compilations.
///     For example a IMethodSymbol defined in one compilation may not be the same in another compilation implementing it.
///     Therefore, the Roslyn SymbolEqualityComparer is not useful for this application.
/// </summary>
public static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat MetadataNameFormat = new(
        SymbolDisplayGlobalNamespaceStyle.Omitted,
        SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
    );

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
    ///     Returns a key for the symbol only without the parent chain.
    ///     This key can identify the overrides of a symbol in a class hierarchy
    /// </summary>
    private static string KeySymbolOnly(this ISymbol symbol)
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

    private static string GetMetadataName(this ISymbol symbol)
    {
        // Note: ISymbol.MetaDataName is not sufficient. It contains for example just the interface name.
        // Compilation.GetTypeByMetadataName is very picky about the given format.
        return symbol.ToDisplayString(MetadataNameFormat);
    }

    public static bool IsFromSource(this ISymbol symbol)
    {
        return symbol.Locations.Any(loc => loc.IsInSource);
    }

    /// <summary>
    ///     Finds the corresponding symbol in the target compilation.
    ///     TODO Does this work for generics, too? See GetMetadataName.
    /// </summary>
    public static ISymbol? FindCorrespondingSymbol(this ISymbol originalSymbol, Compilation targetCompilation)
    {
        // Warning: 
        // SymbolDisplayFormat.FullyQualifiedFormat is not sufficient to find the types via GetTypeByMetadataName!

        ISymbol? correspondingSymbol = null;
        switch (originalSymbol)
        {
            case INamedTypeSymbol:

                // Note: ISymbol.MetadataName is not sufficient.
                var metaDataName = originalSymbol.GetMetadataName();
                correspondingSymbol = targetCompilation.GetTypeByMetadataName(metaDataName);
                break;

            case IMethodSymbol:
            case IPropertySymbol:
            case IEventSymbol:
                if (FindCorrespondingSymbol(originalSymbol.ContainingType, targetCompilation) is INamedTypeSymbol
                    containingType)
                {
                    correspondingSymbol = containingType.GetMembers(originalSymbol.Name)
                        .FirstOrDefault(m => m.KeySymbolOnly() == originalSymbol.KeySymbolOnly());
                }

                break;
            default:
                Debug.Assert(false);
                break;

            // Add cases for other symbol types as needed (e.g., IFieldSymbol, IPropertySymbol, etc.)
        }

        return correspondingSymbol;
    }

    public static Compilation FindCompilation(this ISymbol symbol)
    {
        while (true)
        {
            if (symbol is INamespaceSymbol { ContainingCompilation: not null } ns)
            {
                return ns.ContainingCompilation;
            }

            if (symbol is ISourceAssemblySymbol { Compilation: not null } sas)
            {
                return sas.Compilation;
            }

            symbol = symbol.ContainingSymbol ?? throw new Exception("No compilation");
        }
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