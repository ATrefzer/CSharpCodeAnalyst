using Contracts.Graph;
using Microsoft.CodeAnalysis;

namespace CodeParser.Parser;

/// <summary>
///     In pass 1 only internal code elements are created.
///     External dependencies are created on the fly in pass 2
/// </summary>
internal class ExternalCodeElementCache
{
    /// <summary>
    ///     Cache for external code elements created on-demand.
    ///     Key: symbol key from ISymbol.Key()
    ///     Value: CodeElement representing the external type/member
    /// </summary>
    private readonly Dictionary<string, CodeElement> _externalElementCache = new();

    private readonly object _lock = new();

    public IEnumerable<CodeElement> GetCodeElements()
    {
        lock (_externalElementCache)
        {
            return _externalElementCache.Values;
        }
    }

    /// <summary>
    ///     External elements are created with full hierarchy (Method -> Class -> Namespace -> Assembly).
    ///     For generic types, always uses the original definition (List&lt;T&gt; not List&lt;int&gt;).
    /// </summary>
    public CodeElement? TryGetOrCreateExternalCodeElement(ISymbol symbol)
    {
        var elementType = DetermineCodeElementType(symbol);
        if (!IsSupportedExternalElementType(elementType))
        {
            return null;
        }

        var symbolToUse = symbol;
        var symbolKey = symbolToUse.Key();

        lock (_lock)
        {
            // Check if we've already created an external element for this symbol
            if (_externalElementCache.TryGetValue(symbolKey, out var cachedExternal))
            {
                return cachedExternal;
            }

            // Create a new external element with hierarchy
            return TryCreateExternalCodeElementWithHierarchy(symbolToUse);
        }
    }

    private bool IsSupportedExternalElementType(CodeElementType elementType)
    {
        // Get rid of everything I don't know
        return elementType is not CodeElementType.Other;
    }

    /// <summary>
    ///     Creates an external code element with full parent hierarchy up to the assembly.
    ///     Hierarchy: Method -> Class -> Namespace -> Assembly (all marked as external)
    ///     Reuses cached parent elements to avoid duplicates.
    /// </summary>
    private CodeElement? TryCreateExternalCodeElementWithHierarchy(ISymbol symbol)
    {
        // Skip module
        var symbolChain = symbol.GetSymbolChain();

        if (symbolChain.Any(s => !IsSupportedExternalElementType(DetermineCodeElementType(s))))
        {
            // Avoid any unexpected output. Rather lose some information.
            return null;
        }

        // Build from top (assembly) to bottom (symbol)
        symbolChain.Reverse();
        CodeElement? parent = null;
        CodeElement? lastElement = null;
        foreach (var sym in symbolChain)
        {
            if (sym is INamespaceSymbol { IsGlobalNamespace: true })
            {
                // Skip the global namespace. It is added after everything is parsed if necessary.
                continue;
            }
            
            var symbolKey = sym.Key();

            if (!_externalElementCache.TryGetValue(symbolKey, out lastElement))
            {
                lastElement = CreateExternalCodeElement(sym, parent);
                _externalElementCache[symbolKey] = lastElement;
            }

            // New parent for the next iteration
            parent = lastElement;
        }

        return lastElement;
    }



    /// <summary>
    ///     Creates a single external code element for a symbol with the specified parent.
    ///     Does not build the parent hierarchy - use CreateExternalCodeElementWithHierarchy for that.
    /// </summary>
    private CodeElement CreateExternalCodeElement(ISymbol symbol, CodeElement? parent)
    {
        var id = Guid.NewGuid().ToString();
        var name = symbol.Name;
        var fullName = symbol.BuildSymbolName(); // .ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        var elementType = DetermineCodeElementType(symbol);
        var element = new CodeElement(id, elementType, name, fullName, parent)
        {
            IsExternal = true
        };

        // Add child relationship to parent
        parent?.Children.Add(element);

        return element;
    }


    /// <summary>
    ///     Determines the CodeElementType for a symbol.
    /// </summary>
    private static CodeElementType DetermineCodeElementType(ISymbol symbol)
    {
        return symbol switch
        {
            IAssemblySymbol => CodeElementType.Assembly,
            INamespaceSymbol => CodeElementType.Namespace,
            INamedTypeSymbol { TypeKind: TypeKind.Class, IsRecord: true } => CodeElementType.Record,
            INamedTypeSymbol { TypeKind: TypeKind.Class } => CodeElementType.Class,
            INamedTypeSymbol { TypeKind: TypeKind.Interface } => CodeElementType.Interface,
            INamedTypeSymbol { TypeKind: TypeKind.Struct } => CodeElementType.Struct,
            INamedTypeSymbol { TypeKind: TypeKind.Enum } => CodeElementType.Enum,
            INamedTypeSymbol { TypeKind: TypeKind.Delegate } => CodeElementType.Delegate,
            IMethodSymbol => CodeElementType.Method,
            IPropertySymbol => CodeElementType.Property,
            IFieldSymbol => CodeElementType.Field,
            IEventSymbol => CodeElementType.Event,
            _ => CodeElementType.Other
        };
    }
}