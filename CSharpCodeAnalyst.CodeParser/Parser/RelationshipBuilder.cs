using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     The write side of phase 2: resolves symbols to code elements and records relationships in the
///     graph. Owns the single lock that serializes all graph mutations (phase 2 analyzes elements in
///     parallel and relationships may target foreign elements) and the cache for external elements
///     created on demand. One instance per parser run - created by <see cref="RelationshipAnalyzer" />
///     and shared by <see cref="SyntaxNodeAnalyzer" /> and <see cref="DeclarationAnalyzer" />.
/// </summary>
internal class RelationshipBuilder
{
    private readonly Artifacts _artifacts;
    private readonly CodeGraph.Graph.CodeGraph _codeGraph;
    private readonly ParserConfig _config;
    private readonly ExternalCodeElementCache _externalCodeElementCache = new();
    private readonly Lock _lock = new();

    internal RelationshipBuilder(CodeGraph.Graph.CodeGraph codeGraph, Artifacts artifacts, ParserConfig config)
    {
        _codeGraph = codeGraph;
        _artifacts = artifacts;
        _config = config;
    }

    /// <summary>
    ///     The caller has to take care that the symbol is normalized to original definition if necessary
    /// </summary>
    public CodeElement? FindInternalCodeElement(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        _artifacts.SymbolKeyToElementMap.TryGetValue(symbol.Key(), out var element);
        return element;
    }

    public void AddRelationship(CodeElement source, RelationshipType type,
        CodeElement target,
        List<SourceLocation> sourceLocations, RelationshipAttribute attributes)
    {
        lock (_lock)
        {
            var existingRelationship = source.Relationships.FirstOrDefault(d =>
                d.TargetId == target.Id && d.Type == type);

            if (existingRelationship != null)
            {
                // Note we may read some relationships more than once through different ways but that's fine.
                // For example identifier and member access of field.
                var newLocations = sourceLocations.Except(existingRelationship.SourceLocations);
                existingRelationship.SourceLocations.AddRange(newLocations);

                // We may get different attributes from different calls.
                existingRelationship.Attributes |= attributes;
            }
            else
            {
                var newRelationship = new Relationship(source.Id, target.Id, type);
                newRelationship.SourceLocations.AddRange(sourceLocations);
                newRelationship.Attributes = attributes;

                source.Relationships.Add(newRelationship);
            }
        }
    }

    /// <summary>
    ///     Adds a synthetic element (e.g. the dummy class/method holding global statements) to the graph.
    /// </summary>
    public void AddElement(CodeElement element, CodeElement parent)
    {
        lock (_lock)
        {
            _codeGraph.Nodes[element.Id] = element;
            parent.Children.Add(element);
        }
    }

    public void AddTypeRelationship(CodeElement sourceElement, ITypeSymbol typeSymbol,
        RelationshipType relationshipType,
        SourceLocation? location = null)
    {
        switch (typeSymbol)
        {
            case IArrayTypeSymbol arrayType:
                // For arrays, we add an "Uses" relationship to the element type. Even if we create them.
                AddTypeRelationship(sourceElement, arrayType.ElementType, RelationshipType.Uses, location);
                break;

            case INamedTypeSymbol namedTypeSymbol:

                AddNamedTypeRelationship(sourceElement, namedTypeSymbol, relationshipType, location);
                break;

            case IPointerTypeSymbol pointerTypeSymbol:
                AddTypeRelationship(sourceElement, pointerTypeSymbol.PointedAtType, RelationshipType.Uses, location);
                break;
            case IFunctionPointerTypeSymbol:

                // The function pointer has a return type and parameters.
                // we could add these relationships here.

                break;
            case IDynamicTypeSymbol:
                // Noting to gain on this branch
                // For example: Dictionary<string, dynamic>
                break;
            default:
                // Handle other type symbols (e.g., type parameters)
                if (FindInternalCodeElement(typeSymbol) is { } targetElement)
                {
                    AddRelationship(sourceElement, relationshipType, targetElement, location != null ? [location] : [], RelationshipAttribute.None);
                }

                break;
        }
    }

    /// <summary>
    ///     Adds a relationship to a symbol, with configurable fallback behavior for external symbols.
    ///     Tries in order: direct symbol → normalized symbol → containing type → external element
    ///     For external symbols, creates relationships to the CONTAINING TYPE only.
    ///     Example: myList.Add(5) -> relationship to List&lt;T&gt; (not to List&lt;T&gt;.Add)
    /// </summary>
    public void AddRelationshipWithFallbackToContainingType(CodeElement sourceElement, ISymbol targetSymbol,
        RelationshipType relationshipType, List<SourceLocation>? locations, RelationshipAttribute attributes)
    {
        locations ??= [];

        // Step 1: Try to find internal element (direct or normalized)
        var targetElement = TryFindInternalElementWithNormalization(targetSymbol);
        if (targetElement != null)
        {
            AddRelationship(sourceElement, relationshipType, targetElement, locations, attributes);
            return;
        }

        // Step 2: Try containing type (for enum values, primary ctor properties, etc.)
        targetElement = TryFindInternalContainingType(targetSymbol);
        if (targetElement != null)
        {
            AddRelationship(sourceElement, relationshipType, targetElement, locations, attributes);
            return;
        }

        // Step 3: Handle external symbols (if configured)
        if (_config.IncludeExternals)
        {
            targetElement = TryCreateExternalElementForSymbol(targetSymbol);
            if (targetElement != null)
            {
                // External relationships always use "Uses" type (not "Calls", "Creates", etc.)
                AddRelationship(sourceElement, RelationshipType.Uses, targetElement, locations, attributes);
            }
        }
    }

    public void AddCallsRelationship(CodeElement sourceElement, IMethodSymbol methodSymbol, SourceLocation location, RelationshipAttribute attributes)
    {
        if (methodSymbol.IsExtensionMethod)
        {
            // Handle calls to extension methods
            methodSymbol = methodSymbol.ReducedFrom ?? methodSymbol;
        }

        // Normalize generic methods to find original definition (only if not already found internally)
        // This preserves any specific instantiations that might exist in our internal map
        if (FindInternalCodeElement(methodSymbol) is null)
        {
            methodSymbol = methodSymbol.NormalizeToOriginalDefinition();
        }

        // If the method is not in our map, we might want to add a relationship to its containing type
        AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Calls, [location], attributes);
    }

    /// <summary>
    ///     Adds the edge for a compiler-synthesized method call that has no invocation syntax of its own:
    ///     query-pattern operators, Deconstruct, foreach GetEnumerator. Same treatment as a spelled-out
    ///     call (AddCallsRelationship): reduce extension methods, normalize generics, fall back to the
    ///     containing type for externals.
    /// </summary>
    public void AddSynthesizedCallRelationship(CodeElement sourceElement, IMethodSymbol method, SyntaxNode node,
        RelationshipType relationshipType)
    {
        var attributes = method.IsExtensionMethod
            ? RelationshipAttribute.IsExtensionMethodCall
            : RelationshipAttribute.None;

        var methodSymbol = method.ReducedFrom ?? method;
        if (FindInternalCodeElement(methodSymbol) is null)
        {
            methodSymbol = methodSymbol.NormalizeToOriginalDefinition();
        }

        AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, relationshipType,
            [node.GetSyntaxLocation()], attributes);
    }

    /// <summary>
    ///     Adds all external elements that were created during parallel processing to the code graph.
    ///     This must be called after parallel processing completes to avoid collection modification issues.
    /// </summary>
    public void FlushExternalElementsToGraph()
    {
        if (!_config.IncludeExternals)
        {
            return;
        }

        foreach (var externalElement in _externalCodeElementCache.GetCodeElements())
        {
            _codeGraph.Nodes[externalElement.Id] = externalElement;
        }
    }

    /// <summary>
    ///     Adds a relationship to a named type (class, interface, struct, etc.).
    ///     Handles both internal and external types, and resolves generic type definitions.
    /// </summary>
    private void AddNamedTypeRelationship(CodeElement sourceElement, INamedTypeSymbol namedTypeSymbol,
        RelationshipType relationshipType,
        SourceLocation? location)
    {
        var targetElement = FindInternalCodeElement(namedTypeSymbol);
        if (targetElement != null)
        {
            // The type is internal (part of our codebase)
            AddRelationship(sourceElement, relationshipType, targetElement, location != null ? [location] : [], RelationshipAttribute.None);
            return;
        }

        // Note the constructed type is not in our CodeElement map!
        // It is not found in phase1 the way we parse it but the original definition is.
        // For constructed generic types (List<int>), use the original definition (List<T>)
        var normalizedSymbol = namedTypeSymbol.NormalizeToOriginalDefinition();

        targetElement = FindInternalCodeElement(normalizedSymbol);
        if (targetElement == null && _config.IncludeExternals)
        {
            targetElement = TryGetOrCreateExternalCodeElement(normalizedSymbol);
        }

        if (targetElement is not null)
        {
            AddRelationship(sourceElement, relationshipType, targetElement, location != null ? [location] : [], RelationshipAttribute.None);
        }

        // For generic types, add "Uses" relationships to type arguments
        if (namedTypeSymbol.IsGenericType)
        {
            foreach (var typeArg in namedTypeSymbol.TypeArguments)
            {
                // A type parameterized with itself (records implement IEquatable<Self>; CRTP like
                // class Foo : IComparable<Foo>) would otherwise gain a meaningless self-reference.
                if (typeArg is INamedTypeSymbol namedTypeArg &&
                    ReferenceEquals(FindInternalCodeElement(namedTypeArg.NormalizeToOriginalDefinition()), sourceElement))
                {
                    continue;
                }

                AddTypeRelationship(sourceElement, typeArg, RelationshipType.Uses, location);
            }
        }
    }

    private CodeElement? TryGetOrCreateExternalCodeElement(INamedTypeSymbol symbol)
    {
        // Just because we did not find the symbol does not mean it is external for sure. There
        // is are lot of unnamed things around.
        if (symbol.IsFromSource())
        {
            return null;
        }

        return _externalCodeElementCache.TryGetOrCreateExternalCodeElement(symbol);
    }

    /// <summary>
    ///     Tries to find an internal element for the symbol, with normalization fallback.
    ///     Handles constructed generics (List&lt;int&gt; → List&lt;T&gt;).
    /// </summary>
    private CodeElement? TryFindInternalElementWithNormalization(ISymbol symbol)
    {
        // Try direct lookup first
        var element = FindInternalCodeElement(symbol);
        if (element != null)
        {
            return element;
        }

        // Try normalized version (for constructed generics)
        // NormalizeToOriginalDefinition might return the same symbol or a normalized version
        // The Key() method will determine uniqueness across compilations
        var normalizedSymbol = symbol.NormalizeToOriginalDefinition();
        return FindInternalCodeElement(normalizedSymbol);
    }

    /// <summary>
    ///     Tries to find the containing type as an internal element.
    ///     Used for: enum values, primary constructor properties, etc.
    /// </summary>
    private CodeElement? TryFindInternalContainingType(ISymbol? symbol)
    {
        if (symbol?.ContainingType == null)
        {
            return null;
        }

        return FindInternalCodeElement(symbol.ContainingType);
    }

    /// <summary>
    ///     Creates or retrieves an external element for the symbol.
    ///     Always returns the containing TYPE element (not method/property/field level).
    ///     Returns null if the symbol is from source code or if external element creation fails.
    /// </summary>
    private CodeElement? TryCreateExternalElementForSymbol(ISymbol symbol)
    {
        // Extract the type symbol (symbol itself if it's a type, otherwise its containing type)
        var typeSymbol = GetTypeSymbolForExternal(symbol);
        if (typeSymbol == null)
        {
            return null;
        }

        // Normalize to original definition (List<int> → List<T>)
        typeSymbol = typeSymbol.NormalizeToOriginalDefinition();

        return TryGetOrCreateExternalCodeElement(typeSymbol);
    }

    /// <summary>
    ///     Extracts the type symbol to use for external element creation.
    ///     - If symbol is a type, returns it
    ///     - If symbol is a member (method/property/field), returns its containing type
    /// </summary>
    private static INamedTypeSymbol? GetTypeSymbolForExternal(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            { ContainingType: not null } => symbol.ContainingType,
            _ => null
        };
    }
}
