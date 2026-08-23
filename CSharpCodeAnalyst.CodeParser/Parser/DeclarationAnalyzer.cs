using System.Diagnostics;
using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     The declaration side of phase 2: everything a code element depends on through its *signature and
///     declaration* - parameter/return/field/property types, inheritance, interface implementations,
///     overrides, attributes, enum member initializers and primary-constructor base calls. Bodies
///     (methods, accessors, initializers, attribute arguments) are handed to the
///     <see cref="SyntaxNodeAnalyzer" />, edges to the <see cref="RelationshipBuilder" />.
/// </summary>
internal class DeclarationAnalyzer
{
    private readonly Artifacts _artifacts;
    private readonly SyntaxNodeAnalyzer _bodyAnalyzer;
    private readonly RelationshipBuilder _builder;
    private readonly ParserConfig _config;
    private readonly ExternalContractStore _externalContracts;

    internal DeclarationAnalyzer(RelationshipBuilder builder, SyntaxNodeAnalyzer bodyAnalyzer, Artifacts artifacts,
        ParserConfig config, ExternalContractStore externalContracts)
    {
        _builder = builder;
        _bodyAnalyzer = bodyAnalyzer;
        _artifacts = artifacts;
        _config = config;
        _externalContracts = externalContracts;
    }

    /// <summary>
    ///     Analyzes all relationships of a single code element. Called in parallel (one element per call);
    ///     everything that writes to the graph goes through the builder, which serializes the mutations.
    /// </summary>
    public void Analyze(Solution solution, CodeElement element, ISymbol symbol)
    {
        if (symbol is IEventSymbol eventSymbol)
        {
            AnalyzeEventRelationships(solution, element, eventSymbol);
        }
        else if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateSymbol)
        {
            // Handle before the type relationships.
            AnalyzeDelegateRelationships(element, delegateSymbol);
        }
        else if (symbol is INamedTypeSymbol typeSymbol)
        {
            AnalyzeInheritanceRelationships(element, typeSymbol);
            AnalyzeEnumMemberInitializers(solution, element, typeSymbol);
            RecordExternalInterfaceImplementations(typeSymbol);
            RecordIfNotifyingType(element, typeSymbol);
        }
        else if (symbol is IMethodSymbol methodSymbol)
        {
            AnalyzeMethodRelationships(solution, element, methodSymbol);
        }
        else if (symbol is IPropertySymbol propertySymbol)
        {
            AnalyzePropertyRelationships(solution, element, propertySymbol);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            AnalyzeFieldRelationships(solution, element, fieldSymbol);
        }
        else if (symbol is IParameterSymbol parameterSymbol)
        {
            // A captured primary constructor parameter - phase 1 gave it a Field element (see
            // HierarchyAnalyzer.CreateCapturedParameterField). There is no initializer to walk, only
            // its own type, like any other field.
            _builder.AddTypeRelationship(element, parameterSymbol.Type, RelationshipType.Uses,
                parameterSymbol.GetSymbolLocations().FirstOrDefault());
        }

        // For all type of symbols check if decorated with an attribute.
        AnalyzeAttributeRelationships(solution, element, symbol);
    }

    /// <summary>
    ///     Enum members are deliberately not code elements (references to them fall back to the enum
    ///     type), so their initializer expressions ("Highest = Limits.Max") are walked here and the
    ///     dependencies anchored on the enum element itself.
    /// </summary>
    private void AnalyzeEnumMemberInitializers(Solution solution, CodeElement enumElement, INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind != TypeKind.Enum)
        {
            return;
        }

        foreach (var syntaxReference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not EnumDeclarationSyntax enumDeclaration)
            {
                continue;
            }

            SemanticModel? semanticModel = null;
            foreach (var member in enumDeclaration.Members)
            {
                if (member.EqualsValue is null)
                {
                    continue;
                }

                semanticModel ??= solution.GetDocument(enumDeclaration.SyntaxTree)?.GetSemanticModelAsync().Result;
                if (semanticModel is null)
                {
                    break;
                }

                _bodyAnalyzer.AnalyzeMethodBody(enumElement, member.EqualsValue, semanticModel);
            }
        }
    }

    private void AnalyzeAttributeRelationships(Solution solution, CodeElement element, ISymbol symbol)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass != null)
            {
                var attributeSyntax = attributeData.ApplicationSyntaxReference?.GetSyntax();
                var location = attributeSyntax?.GetSyntaxLocation();

                element.Attributes.Add(attributeData.AttributeClass.Name);
                _builder.AddTypeRelationship(element, attributeData.AttributeClass, RelationshipType.UsesAttribute, location);

                // Attribute arguments (typeof(...), constants) are real dependencies. Method
                // declarations are walked as a whole in AnalyzeMethodRelationships including their
                // attribute lists, so only the other element kinds need the walk here.
                if (symbol is not IMethodSymbol &&
                    attributeSyntax is AttributeSyntax { ArgumentList: not null } attributeWithArguments)
                {
                    var semanticModel = solution.GetDocument(attributeWithArguments.SyntaxTree)?.GetSemanticModelAsync().Result;
                    if (semanticModel is not null)
                    {
                        _bodyAnalyzer.AnalyzeMethodBody(element, attributeWithArguments.ArgumentList, semanticModel);
                    }
                }
            }
        }
    }

    private void AnalyzeDelegateRelationships(CodeElement delegateElement, INamedTypeSymbol delegateSymbol)
    {
        var methodSymbol = delegateSymbol.DelegateInvokeMethod;
        if (methodSymbol is null)
        {
            Trace.WriteLine("Method symbol not available for delegate");
            return;
        }

        // Get delegate declaration location
        var delegateLocations = delegateSymbol.GetSymbolLocations();
        var delegateLocation = delegateLocations.FirstOrDefault();

        // Analyze return type
        _builder.AddTypeRelationship(delegateElement, methodSymbol.ReturnType, RelationshipType.Uses, delegateLocation);

        // Analyze parameter types
        foreach (var parameter in methodSymbol.Parameters)
        {
            _builder.AddTypeRelationship(delegateElement, parameter.Type, RelationshipType.Uses, delegateLocation);
        }
    }

    private void AnalyzeEventRelationships(Solution solution, CodeElement eventElement, IEventSymbol eventSymbol)
    {
        // Get event declaration location
        var eventLocations = eventSymbol.GetSymbolLocations();
        var eventLocation = eventLocations.FirstOrDefault();

        // Analyze event type (usually a delegate type)
        _builder.AddTypeRelationship(eventElement, eventSymbol.Type, RelationshipType.Uses, eventLocation);

        if (eventSymbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            AddImplementationsForInterfaceMember(eventElement, eventSymbol);
        }

        // If the event has "add"/"remove" accessors, analyze them
        if (eventSymbol.AddMethod != null)
        {
            AnalyzeMethodRelationships(solution, eventElement, eventSymbol.AddMethod);
        }

        if (eventSymbol.RemoveMethod != null)
        {
            AnalyzeMethodRelationships(solution, eventElement, eventSymbol.RemoveMethod);
        }
    }

    /// <summary>
    ///     Use solution, not the compilation. The syntax tree may not be found.
    /// </summary>
    private void AnalyzeMethodRelationships(Solution solution, CodeElement methodElement, IMethodSymbol methodSymbol)
    {
        // Get method declaration location for parameter and return type references
        var methodLocations = methodSymbol.GetSymbolLocations();
        var methodLocation = methodLocations.FirstOrDefault();

        // Analyze parameter types
        foreach (var parameter in methodSymbol.Parameters)
        {
            _builder.AddTypeRelationship(methodElement, parameter.Type, RelationshipType.Uses, methodLocation);
        }

        // Analyze generic type-parameter constraints (where T : Foo)
        AnalyzeTypeParameterConstraints(methodElement, methodSymbol.TypeParameters, methodLocation);

        // Analyze return type
        if (!methodSymbol.ReturnsVoid)
        {
            _builder.AddTypeRelationship(methodElement, methodSymbol.ReturnType, RelationshipType.Uses, methodLocation);
        }

        // If this method is an interface method, find its implementations
        if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            AddImplementationsForInterfaceMember(methodElement, methodSymbol);
        }

        // Check for method override
        if (methodSymbol.IsOverride)
        {
            var overriddenMethod = methodSymbol.OverriddenMethod;
            if (overriddenMethod != null)
            {
                var locations = methodSymbol.GetSymbolLocations();
                AddMethodOverrideRelationship(methodElement, overriddenMethod, locations);
            }
        }

        // Analyze method body for object creations and method calls.
        // For a partial method both parts are walked - the stored symbol may be the body-less
        // definition part (see GetDeclaringSyntaxReferencesIncludingPartial).
        foreach (var syntaxReference in methodSymbol.GetDeclaringSyntaxReferencesIncludingPartial())
        {
            var syntax = syntaxReference.GetSyntax();
            var document = solution.GetDocument(syntax.SyntaxTree);

            var semanticModel = document?.GetSemanticModelAsync().Result;
            if (semanticModel == null)
            {
                continue;
            }

            if (syntax is TypeDeclarationSyntax typeDeclaration)
            {
                // A primary constructor's declaring syntax is the type declaration. Walking that as a
                // body would attribute every member of the type to the constructor, so only what the
                // constructor actually runs is analyzed here.
                AnalyzePrimaryConstructorBody(methodElement, typeDeclaration, semanticModel);
                continue;
            }

            _bodyAnalyzer.AnalyzeMethodBody(methodElement, syntax, semanticModel);
        }
    }

    /// <summary>
    ///     What a primary constructor runs: the argument list of its base-type clause
    ///     ("class Derived(int n) : Base(Helper.Scale(n))"). The parameter types are already covered by
    ///     the ordinary parameter handling, and the field/property initializers are anchored on those
    ///     members themselves.
    ///     <para>
    ///         The call to the base constructor gets the same guard as
    ///         <c>AnalyzeConstructorInitializer</c>: it is linked when the target is an element of the
    ///         graph.
    ///     </para>
    /// </summary>
    private void AnalyzePrimaryConstructorBody(CodeElement methodElement, TypeDeclarationSyntax typeDeclaration,
        SemanticModel semanticModel)
    {
        if (typeDeclaration.BaseList is null)
        {
            return;
        }

        foreach (var baseType in typeDeclaration.BaseList.Types.OfType<PrimaryConstructorBaseTypeSyntax>())
        {
            if (semanticModel.GetSymbolInfo(baseType).Symbol is
                IMethodSymbol { MethodKind: MethodKind.Constructor, IsImplicitlyDeclared: false } baseConstructor)
            {
                var normalizedConstructor = baseConstructor.NormalizeToOriginalDefinition();
                if (_builder.FindInternalCodeElement(normalizedConstructor) is not null)
                {
                    _builder.AddCallsRelationship(methodElement, normalizedConstructor, baseType.GetSyntaxLocation(),
                        RelationshipAttribute.IsBaseCall);
                }
            }

            // The argument expressions run at construction - normal body semantics.
            _bodyAnalyzer.AnalyzeMethodBody(methodElement, baseType.ArgumentList, semanticModel);
        }
    }

    /// <summary>
    ///     Adds "implements" relationships for interface members
    /// </summary>
    private void AddImplementationsForInterfaceMember(CodeElement element, ISymbol symbol)
    {
        var implementingTypes = new HashSet<INamedTypeSymbol>();

        if (symbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            // If it's an interface method, find all types implementing the interface
            implementingTypes.UnionWith(FindTypesImplementingInterface(symbol.ContainingType));
        }

        foreach (var implementingType in implementingTypes)
        {
            // Note: We may get a positive answer for all implementing types even if we expect exactly one type to implement it.
            // That's ok the relationship is established only once. It is always the same implementation that is found.

            foreach (var implementingSymbol in FindImplementationsForInterfaceMember(symbol, implementingType))
            {
                var implementingElement = _builder.FindInternalCodeElement(implementingSymbol);
                if (implementingElement is null)
                {
                    // Note: Implementations for external methods are not in our map
                    continue;
                }

                if (ReferenceEquals(implementingElement, element))
                {
                    // A class that inherits a default interface method reports the interface member
                    // itself as the implementation - that must not become an Implements self edge.
                    continue;
                }

                var locations = implementingSymbol.GetSymbolLocations();
                _builder.AddRelationship(implementingElement, RelationshipType.Implements, element, locations, RelationshipAttribute.None);
            }
        }
    }

    /// <summary>
    ///     For methods, properties (accessors) and events. Searches the whole hierarchy of the
    ///     implementing type and returns the symbols implementing the given interface member - one per
    ///     construction of the interface (DualHandler : IHandler&lt;A&gt;, IHandler&lt;B&gt; implements
    ///     IHandler&lt;T&gt;.Handle with two different methods). Later overrides are ignored here.
    ///     Roslyn trap: <see cref="INamedTypeSymbol.FindImplementationForInterfaceMember" /> must be
    ///     called with the member of the CONSTRUCTED interface the type actually implements. Our phase-1
    ///     symbol is the member of the interface DEFINITION - it only works directly when the interface is
    ///     not generic (definition and construction coincide). So the definition member is first mapped
    ///     onto every matching construction in AllInterfaces. The key comparison also bridges
    ///     compilations, so an interface defined in another project resolves without extra mapping.
    /// </summary>
    private static IEnumerable<ISymbol> FindImplementationsForInterfaceMember(ISymbol symbol,
        INamedTypeSymbol implementingType)
    {
        // symbol is from phase 1. It is a definition, nothing constructed.
        var results = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        var definition = symbol.OriginalDefinition;
        var definitionKey = definition.Key();
        var interfaceKey = definition.ContainingType.Key();

        foreach (var constructedInterface in implementingType.AllInterfaces)
        {
            if (constructedInterface.OriginalDefinition.Key() != interfaceKey)
            {
                // Not the requested interface.
                continue;
            }

            var constructedMember = constructedInterface.GetMembers(symbol.Name)
                .FirstOrDefault(m => m.OriginalDefinition.Key() == definitionKey);
            if (constructedMember is null)
            {
                continue;
            }

            var implementation = implementingType.FindImplementationForInterfaceMember(constructedMember);
            if (implementation is not null)
            {
                results.Add(implementation);
            }
        }

        return results;
    }

    /// <summary>
    ///     Returns the named types that directly implement the given interface.
    ///     Interfaces implemented in base types are not considered.
    /// </summary>
    private IEnumerable<INamedTypeSymbol> FindTypesImplementingInterface(INamedTypeSymbol interfaceSymbol)
    {
        // The interface-key -> implementing-types map is precomputed in phase 1 (see Artifacts). It already
        // accounts for interfaces implemented in a base type, since it is built from AllInterfaces.
        var interfaceKey = interfaceSymbol.Key();
        return _artifacts.InterfaceImplementations.GetValueOrDefault(interfaceKey) ?? [];
    }

    /// <summary>
    ///     Overrides
    /// </summary>
    private void AddMethodOverrideRelationship(CodeElement sourceElement, IMethodSymbol methodSymbol,
        List<SourceLocation> locations)
    {
        // If we don't have the method itself in our map, add a relationship to its containing type
        // Maybe we override a framework method. Happens also if the base method is a generic one.
        // In this case the GetSymbolKey is different. One uses T, the overriding method uses the actual type.
        _builder.AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Overrides, locations, RelationshipAttribute.None);

        RecordIfExternalContract(sourceElement, methodSymbol);
    }

    /// <summary>
    ///     An override whose base member lives outside the analyzed code produces no relationship at all -
    ///     there is no element to point at - so the member ends up without a single incoming reference and
    ///     looks like dead code. The fact is recorded beside the graph instead.
    ///     The containing type decides: when it is one of ours the contract is internal, and the
    ///     <see cref="RelationshipType.Overrides" /> edge already expresses it.
    /// </summary>
    private void RecordIfExternalContract(CodeElement sourceElement, ISymbol contractMember)
    {
        var containingType = contractMember.ContainingType;
        if (containingType is null || _builder.FindInternalCodeElement(containingType.OriginalDefinition) is not null)
        {
            return;
        }

        _externalContracts.Add(sourceElement.Id, $"{containingType.Name}.{contractMember.Name}");
    }

    /// <summary>
    ///     Members that implement an interface from outside the analyzed code (<c>ICommand.Execute</c>,
    ///     <c>IValueConverter.Convert</c>, ...). Nothing in the graph shows this: the interface is not an
    ///     element, and the implementation is called by the framework, never from our code.
    ///     <para>
    ///         Only foreign interfaces are scanned. For our own, <see cref="AddImplementationsForInterfaceMember" />
    ///         creates real <see cref="RelationshipType.Implements" /> edges from the interface side.
    ///     </para>
    ///     <para>
    ///         The interfaces come from <see cref="INamedTypeSymbol.AllInterfaces" /> and are therefore
    ///         already constructed, so <see cref="INamedTypeSymbol.FindImplementationForInterfaceMember" />
    ///         can be called directly - the mapping trap described at
    ///         <see cref="FindImplementationsForInterfaceMember" /> does not apply here.
    ///     </para>
    /// </summary>
    private void RecordExternalInterfaceImplementations(INamedTypeSymbol typeSymbol)
    {
        foreach (var contract in typeSymbol.AllInterfaces)
        {
            // A constructed generic interface (IHandler<Widget>) is not in the map - the definition is.
            if (_builder.FindInternalCodeElement(contract.OriginalDefinition) is not null)
            {
                continue;
            }

            foreach (var contractMember in contract.GetMembers())
            {
                var implementation = typeSymbol.FindImplementationForInterfaceMember(contractMember);
                if (implementation is null)
                {
                    continue;
                }

                var element = _builder.FindInternalCodeElement(implementation)
                              ?? _builder.FindInternalCodeElement(implementation.OriginalDefinition);
                if (element is not null)
                {
                    _externalContracts.Add(element.Id, $"{contract.Name}.{contractMember.Name}");
                }
            }
        }
    }

    /// <summary>
    ///     Records a type that raises change notifications - <c>INotifyPropertyChanged</c> appears
    ///     anywhere in its interface set, no matter which class of the inheritance chain implements it.
    ///     The member-level contract cannot carry this fact when the implementation sits in a base class
    ///     outside the analyzed code (ObservableObject, BindableBase, ...): the derived type then has no
    ///     PropertyChanged member of its own, so from the graph alone nothing says it is a view model,
    ///     and the dead code analysis would rate its bindable properties too confidently.
    /// </summary>
    private void RecordIfNotifyingType(CodeElement element, INamedTypeSymbol typeSymbol)
    {
        var raisesChangeNotifications = typeSymbol.AllInterfaces.Any(contract => contract is
        {
            Name: "INotifyPropertyChanged",
            ContainingNamespace: { Name: "ComponentModel", ContainingNamespace.Name: "System" }
        });

        if (raisesChangeNotifications)
        {
            _externalContracts.AddNotifyingType(element.Id);
        }
    }

    private void AnalyzeFieldRelationships(Solution solution, CodeElement fieldElement, IFieldSymbol fieldSymbol)
    {
        // Get field declaration location
        var fieldLocations = fieldSymbol.GetSymbolLocations();
        var fieldLocation = fieldLocations.FirstOrDefault();

        _builder.AddTypeRelationship(fieldElement, fieldSymbol.Type, RelationshipType.Uses, fieldLocation);

        // Analyze field initializer
        foreach (var syntaxReference in fieldSymbol.DeclaringSyntaxReferences)
        {
            var syntax = syntaxReference.GetSyntax();

            // VariableDeclaratorSyntax for fields
            if (syntax is VariableDeclaratorSyntax { Initializer: not null } variableDeclarator)
            {
                var document = solution.GetDocument(syntax.SyntaxTree);
                var semanticModel = document?.GetSemanticModelAsync().Result;
                if (semanticModel != null)
                {
                    // The walk starts at the EqualsValueClause (not its value) so that an implicit
                    // user-defined conversion of the initializer is captured (VisitEqualsValueClause).
                    _bodyAnalyzer.AnalyzeMethodBody(fieldElement, variableDeclarator.Initializer, semanticModel);
                }
            }
        }
    }

    /// <summary>
    ///     Handle also List_T. Where List is not a code element of our project
    /// </summary>
    private void AnalyzeInheritanceRelationships(CodeElement element, INamedTypeSymbol typeSymbol)
    {
        // Get type declaration location for inheritance relationships
        var typeLocations = typeSymbol.GetSymbolLocations();
        var typeLocation = typeLocations.FirstOrDefault();

        // Analyze base class
        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            _builder.AddTypeRelationship(element, typeSymbol.BaseType, RelationshipType.Inherits, typeLocation);
        }

        // Analyze implemented interfaces
        foreach (var @interface in typeSymbol.Interfaces)
        {
            _builder.AddTypeRelationship(element, @interface, RelationshipType.Implements, typeLocation);
        }

        // Analyze generic type-parameter constraints (where T : Foo)
        AnalyzeTypeParameterConstraints(element, typeSymbol.TypeParameters, typeLocation);
    }

    /// <summary>
    ///     Records "Uses" relationships for the type constraints of generic type parameters
    ///     (where T : IFoo / where T : BaseClass). Special constraints (class, struct, new(),
    ///     notnull) carry no type and a constraint to another type parameter (where T : U) resolves
    ///     to no internal element, so both are naturally ignored.
    /// </summary>
    private void AnalyzeTypeParameterConstraints(CodeElement element,
        IEnumerable<ITypeParameterSymbol> typeParameters, SourceLocation? location)
    {
        foreach (var typeParameter in typeParameters)
        {
            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                _builder.AddTypeRelationship(element, constraintType, RelationshipType.Uses, location);
            }
        }
    }

    /// <summary>
    ///     Properties became quite complex.
    ///     We treat the property like a method and do not distinguish between getter and setter.
    ///     A property can have a getter, setter or an expression body.
    /// </summary>
    private void AnalyzePropertyRelationships(Solution solution, CodeElement propertyElement,
        IPropertySymbol propertySymbol)
    {
        // Get property declaration location
        var propertyLocations = propertySymbol.GetSymbolLocations();
        var propertyLocation = propertyLocations.FirstOrDefault();

        // Analyze the property type
        _builder.AddTypeRelationship(propertyElement, propertySymbol.Type, RelationshipType.Uses, propertyLocation);

        // Indexer parameter types. Empty for normal properties.
        foreach (var parameter in propertySymbol.Parameters)
        {
            _builder.AddTypeRelationship(propertyElement, parameter.Type, RelationshipType.Uses, propertyLocation);
        }

        // Interface implementation and override relationships. When accessors are split these are
        // modeled at the accessor level (get/set), otherwise on the property element.
        AnalyzePropertyAbstractions(propertyElement, propertySymbol);

        // Analyze the property body (including accessors)
        AnalyzePropertyBody(solution, propertyElement, propertySymbol);
    }

    /// <summary>
    ///     Creates the Implements (interface) and Overrides relationships for a property.
    ///     When accessors are split, these are modeled at the accessor level (a getter implements/overrides
    ///     a getter, a setter a setter), so the abstraction walk in the explorer and the cycle classifier
    ///     treat them exactly like method implementations/overrides. Without splitting they stay on the
    ///     property element.
    /// </summary>
    private void AnalyzePropertyAbstractions(CodeElement propertyElement, IPropertySymbol propertySymbol)
    {
        if (_config.SplitPropertyAccessors)
        {
            AnalyzeAccessorAbstractions(propertySymbol.GetMethod);
            AnalyzeAccessorAbstractions(propertySymbol.SetMethod);
            return;
        }

        if (propertySymbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            AddImplementationsForInterfaceMember(propertyElement, propertySymbol);
        }

        if (propertySymbol.IsOverride && propertySymbol.OverriddenProperty is { } overriddenProperty)
        {
            _builder.AddRelationshipWithFallbackToContainingType(propertyElement, overriddenProperty,
                RelationshipType.Overrides, propertySymbol.GetSymbolLocations(), RelationshipAttribute.None);

            RecordIfExternalContract(propertyElement, overriddenProperty);
        }
    }

    /// <summary>
    ///     Mirrors the method-level interface/override handling for a single property accessor. The accessor
    ///     is an <see cref="IMethodSymbol" /> (get_Prop / set_Prop), so the existing method machinery applies
    ///     directly: the implementing accessor and the overridden base accessor are both resolved by symbol key.
    /// </summary>
    private void AnalyzeAccessorAbstractions(IMethodSymbol? accessor)
    {
        if (accessor is null)
        {
            return;
        }

        var accessorElement = _builder.FindInternalCodeElement(accessor);
        if (accessorElement is null)
        {
            return;
        }

        if (accessor.ContainingType.TypeKind == TypeKind.Interface)
        {
            AddImplementationsForInterfaceMember(accessorElement, accessor);
        }

        if (accessor.IsOverride && accessor.OverriddenMethod is { } overriddenAccessor)
        {
            AddMethodOverrideRelationship(accessorElement, overriddenAccessor, accessor.GetSymbolLocations());
        }
    }

    private void AnalyzePropertyBody(Solution solution, CodeElement propertyElement, IPropertySymbol propertySymbol)
    {
        // When splitting is enabled, accessor bodies are attributed to their own getter/setter element
        // instead of the property container. The elements were created in phase 1; if (for whatever
        // reason) one is missing we fall back to the property container.
        var getElement = _config.SplitPropertyAccessors
            ? _builder.FindInternalCodeElement(propertySymbol.GetMethod) ?? propertyElement
            : propertyElement;
        var setElement = _config.SplitPropertyAccessors
            ? _builder.FindInternalCodeElement(propertySymbol.SetMethod) ?? propertyElement
            : propertyElement;

        // For a partial property both parts are walked - the stored symbol may be the body-less
        // definition part (see GetDeclaringSyntaxReferencesIncludingPartial).
        foreach (var syntaxReference in propertySymbol.GetDeclaringSyntaxReferencesIncludingPartial())
        {
            var syntax = syntaxReference.GetSyntax();

            // BasePropertyDeclarationSyntax covers both properties and indexers.
            if (syntax is BasePropertyDeclarationSyntax basePropertyDeclaration)
            {
                var document = solution.GetDocument(syntax.SyntaxTree);
                var semanticModel = document?.GetSemanticModelAsync().Result;
                if (semanticModel != null)
                {
                    // The expression body lives on the concrete declaration type, not on the base.
                    var expressionBody = syntax switch
                    {
                        PropertyDeclarationSyntax propertyDeclaration => propertyDeclaration.ExpressionBody,
                        IndexerDeclarationSyntax indexerDeclaration => indexerDeclaration.ExpressionBody,
                        _ => null
                    };

                    if (expressionBody != null)
                    {
                        // An expression-bodied property/indexer is the getter. The walk starts at the
                        // arrow clause (not its expression) so that an implicit user-defined conversion
                        // of the result is captured (VisitArrowExpressionClause).
                        _bodyAnalyzer.AnalyzeMethodBody(getElement, expressionBody, semanticModel);
                    }
                    else if (basePropertyDeclaration.AccessorList != null)
                    {
                        foreach (var accessor in basePropertyDeclaration.AccessorList.Accessors)
                        {
                            // get -> getter element; set / init -> setter element.
                            var accessorElement = accessor.Keyword.IsKind(SyntaxKind.GetKeyword)
                                ? getElement
                                : setElement;

                            if (accessor.ExpressionBody != null)
                            {
                                _bodyAnalyzer.AnalyzeMethodBody(accessorElement, accessor.ExpressionBody, semanticModel);
                            }
                            else if (accessor.Body != null)
                            {
                                _bodyAnalyzer.AnalyzeMethodBody(accessorElement, accessor.Body, semanticModel);
                            }
                        }
                    }

                    // Property initializer: public Foo Bar { get; } = new Foo();
                    // An auto-property can have both an accessor list and an initializer, so this is
                    // independent of the branch above. Treated like a field initializer: the containing
                    // type "creates" the object, the property "uses" it. Indexers cannot have one.
                    // The initializer runs at construction, so it stays on the property container.
                    if (syntax is PropertyDeclarationSyntax { Initializer: not null } propertyWithInitializer)
                    {
                        _bodyAnalyzer.AnalyzeMethodBody(propertyElement, propertyWithInitializer.Initializer, semanticModel);
                    }
                }
            }
        }
    }
}
