using Contracts.Graph;
using Microsoft.CodeAnalysis;

namespace CodeParser.Parser;

/// <summary>
/// In pass 1 only internal code elements are created.
/// External dependencies are created on the fly in pass 2
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
    ///   External elements are created with full hierarchy (Method -> Class -> Namespace -> Assembly).
    ///   For generic types, always uses the original definition (List&lt;T&gt; not List&lt;int&gt;).
    /// </summary>
 
    public CodeElement GetOrCreateExternalCodeElement(ISymbol symbol)
    {
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
            var externalElement = CreateExternalCodeElementWithHierarchy(symbolToUse);


            // Add to cache only - will be added to graph after parallel processing completes
            _externalElementCache[symbolKey] = externalElement;
            return externalElement;
        }
    }

    /// <summary>
    ///     Creates an external code element with full parent hierarchy up to the assembly.
    ///     Hierarchy: Method -> Class -> Namespace -> Assembly (all marked as external)
    ///     Reuses cached parent elements to avoid duplicates.
    /// </summary>
    private CodeElement CreateExternalCodeElementWithHierarchy(ISymbol symbol)
    {
        // Build the hierarchy from top to bottom (Assembly -> Namespace -> Type -> Member)
        // Start by ensuring all parents exist
        CodeElement? parent = null;

        // Create assembly element
        if (symbol.ContainingAssembly != null)
        {
            parent = GetOrCreateExternalParent(symbol.ContainingAssembly);
        }

        // Create namespace chain
        if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
        {
            parent = GetOrCreateExternalParent(symbol.ContainingNamespace, parent);
        }

        // Create containing type (if this symbol is a member)
        if (symbol.ContainingType != null)
        {
            parent = GetOrCreateExternalParent(symbol.ContainingType, parent);
        }

        // Finally, create the element itself
        return CreateExternalCodeElement(symbol, parent);
    }

    /// <summary>
    ///     Gets or creates a parent element (assembly, namespace, or type) for external symbols.
    ///     Checks the cache first to reuse existing parents.
    /// </summary>
    private CodeElement GetOrCreateExternalParent(ISymbol parentSymbol, CodeElement? grandparent = null)
    {
        var symbolKey = parentSymbol.Key();

        // Check if we've already created this parent
        if (_externalElementCache.TryGetValue(symbolKey, out var existing))
        {
            return existing;
        }

        // Create new parent element
        var parent = CreateExternalCodeElement(parentSymbol, grandparent);
        _externalElementCache[symbolKey] = parent;

        return parent;
    }

    /// <summary>
    ///     Creates a single external code element for a symbol with the specified parent.
    ///     Does not build the parent hierarchy - use CreateExternalCodeElementWithHierarchy for that.
    /// </summary>
    private CodeElement CreateExternalCodeElement(ISymbol symbol, CodeElement? parent)
    {
        var id = Guid.NewGuid().ToString();
        var name = symbol.Name;
        var fullName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
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