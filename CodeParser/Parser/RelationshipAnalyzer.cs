using System.Diagnostics;
using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Phase 2/2 of the parser: Analyzing relationships between code elements.
/// </summary>
public class RelationshipAnalyzer
{
    private readonly object _lock = new();
    private readonly Progress _progress;
    private Artifacts? _artifacts;
    private CodeGraph? _codeGraph;
    private long _lastProgress;

    private int _processedCodeElements;

    /// <summary>
    ///     Phase 2/2 of the parser: Analyzing relationships between code elements.
    /// </summary>
    public RelationshipAnalyzer(Progress progress)
    {
        _progress = progress;
    }

    /// <summary>
    ///     Entry for relationship analysis.
    ///     The code graph is updated in place.
    /// </summary>
    public Task AnalyzeRelationshipsMultiThreaded(Solution solution, CodeGraph codeGraph, Artifacts artifacts)
    {
        ArgumentNullException.ThrowIfNull(solution, nameof(solution));
        ArgumentNullException.ThrowIfNull(codeGraph, nameof(codeGraph));
        ArgumentNullException.ThrowIfNull(artifacts, nameof(artifacts));


        _codeGraph = codeGraph;
        _artifacts = artifacts;

        var numberOfCodeElements = _codeGraph.Nodes.Count;
        _processedCodeElements = 0;

        Parallel.ForEach(_codeGraph.Nodes.Values, AnalyzeRelationshipsLocal);

        // Analyze global statements for each assembly
        AnalyzeGlobalStatementsForAssembly(solution);

        SendParserPhase2Progress(numberOfCodeElements, numberOfCodeElements);

        return Task.CompletedTask;

        void AnalyzeRelationshipsLocal(CodeElement element)
        {
            if (!_artifacts.ElementIdToSymbolMap.TryGetValue(element.Id, out var symbol))
            {
                // INamespaceSymbol
                Interlocked.Increment(ref _processedCodeElements);
                return;
            }

            AnalyzeRelationships(solution, element, symbol);

            var loopValue = Interlocked.Increment(ref _processedCodeElements);
            SendParserPhase2Progress(loopValue, numberOfCodeElements);
        }
    }

    private IEnumerable<INamedTypeSymbol> FindTypesDerivedFrom(INamedTypeSymbol baseType)
    {
        return _artifacts!.AllNamedTypesInSolution
            .Where(type => IsTypeDerivedFrom(type, baseType));
    }

    private static bool IsTypeDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var currentType = type.BaseType;
        while (currentType != null)
        {
            if (currentType.Key() == baseType.Key())
            {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
    }

    /// <summary>
    ///     The code graph is updated, the artifacts are read only.
    /// </summary>
    public Task AnalyzeRelationshipsSingleThreaded(Solution solution, CodeGraph codeGraph, Artifacts artifacts)
    {
        _codeGraph = codeGraph;
        _artifacts = artifacts;

        var numberOfCodeElements = _codeGraph.Nodes.Count;
        var loop = 0;
        foreach (var element in _codeGraph.Nodes.Values)
        {
            loop++;

            if (!_artifacts.ElementIdToSymbolMap.TryGetValue(element.Id, out var symbol))
            {
                // INamespaceSymbol
                continue;
            }

            AnalyzeRelationships(solution, element, symbol);
            SendParserPhase2Progress(loop, numberOfCodeElements);
        }

        // Analyze global statements for each assembly
        AnalyzeGlobalStatementsForAssembly(solution);

        return Task.CompletedTask;
    }

    private void SendParserPhase2Progress(int processed, int total)
    {
        var currentProgress = (long)Math.Floor(processed / (double)total * 100);
        var lastReported = Interlocked.Read(ref _lastProgress);

        if (currentProgress > lastReported)
        {
            if (Interlocked.CompareExchange(ref _lastProgress, currentProgress, lastReported) == lastReported)
            {
                var msg = $"Phase 2/2: Analyzing relationships. Finished {currentProgress}%.";
                _progress.SendProgress(msg);
            }
        }
    }

    private void AnalyzeRelationships(Solution solution, CodeElement element, ISymbol symbol)
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
            AnalyzeFieldRelationships(element, fieldSymbol);
        }

        // For all type of symbols check if decorated with an attribute.
        AnalyzeAttributeRelationships(element, symbol);
    }

    private void AnalyzeGlobalStatementsForAssembly(Solution solution)
    {
        foreach (var (assemblySymbol, globalStatements) in _artifacts!.GlobalStatementsByAssembly)
        {
            if (globalStatements.Count == 0)
            {
                continue;
            }

            // Find the existing assembly element
            var symbolKey = assemblySymbol.Key();
            var assemblyElement = _artifacts.SymbolKeyToElementMap[symbolKey];

            // Create a dummy class for this assembly's global statements
            var dummyClassId = Guid.NewGuid().ToString();
            const string dummyClassName = "GlobalStatements";
            var dummyClassFullName = assemblySymbol.BuildSymbolName() + "." + dummyClassName;
            var dummyClass = new CodeElement(dummyClassId, CodeElementType.Class, dummyClassName, dummyClassFullName,
                assemblyElement);

            lock (_lock)
            {
                _codeGraph!.Nodes[dummyClassId] = dummyClass;
                assemblyElement.Children.Add(dummyClass);
            }

            // Create a dummy method to contain global statements
            var dummyMethodId = Guid.NewGuid().ToString();
            const string dummyMethodName = "Execute";
            var dummyMethodFullName = $"{dummyClassName}.{dummyMethodName}";
            var dummyMethod = new CodeElement(dummyMethodId, CodeElementType.Method, dummyMethodName,
                dummyMethodFullName, dummyClass);

            lock (_lock)
            {
                _codeGraph.Nodes[dummyMethodId] = dummyMethod;
                dummyClass.Children.Add(dummyMethod);
            }

            // Analyze global statements within the context of the dummy method
            foreach (var globalStatement in globalStatements)
            {
                var document = solution.GetDocument(globalStatement.SyntaxTree);
                var semanticModel = document?.GetSemanticModelAsync().Result;
                if (semanticModel != null)
                {
                    AnalyzeMethodBody(dummyMethod, globalStatement, semanticModel);
                }
            }
        }
    }

    private void AnalyzeAttributeRelationships(CodeElement element, ISymbol symbol)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass != null)
            {
                var location = attributeData.ApplicationSyntaxReference?.GetSyntax().GetSyntaxLocation();

                element.Attributes.Add(attributeData.AttributeClass.Name);
                AddTypeRelationship(element, attributeData.AttributeClass, RelationshipType.UsesAttribute, location);
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
        AddTypeRelationship(delegateElement, methodSymbol.ReturnType, RelationshipType.Uses, delegateLocation);

        // Analyze parameter types
        foreach (var parameter in methodSymbol.Parameters)
        {
            AddTypeRelationship(delegateElement, parameter.Type, RelationshipType.Uses, delegateLocation);
        }
    }

    private void AnalyzeEventRelationships(Solution solution, CodeElement eventElement, IEventSymbol eventSymbol)
    {
        // Get event declaration location
        var eventLocations = eventSymbol.GetSymbolLocations();
        var eventLocation = eventLocations.FirstOrDefault();

        // Analyze event type (usually a delegate type)
        AddTypeRelationship(eventElement, eventSymbol.Type, RelationshipType.Uses, eventLocation);


        if (eventSymbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            FindImplementationsForInterfaceMember(eventElement, eventSymbol);
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
            AddTypeRelationship(methodElement, parameter.Type, RelationshipType.Uses, methodLocation);
        }

        // Analyze return type
        if (!methodSymbol.ReturnsVoid)
        {
            AddTypeRelationship(methodElement, methodSymbol.ReturnType, RelationshipType.Uses, methodLocation);
        }

        //if (methodSymbol.IsExtensionMethod)
        //{
        //    // The first parameter of an extension method is the extended type
        //    var extendedType = methodSymbol.Parameters[0].Type;
        //    AddTypeRelationship(methodElement, extendedType, RelationshipType.Uses);
        //}

        // If this method is an interface method, find its implementations
        if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            FindImplementationsForInterfaceMember(methodElement, methodSymbol);
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

        // Analyze method body for object creations and method calls
        foreach (var syntaxReference in methodSymbol.DeclaringSyntaxReferences)
        {
            var syntax = syntaxReference.GetSyntax();
            var document = solution.GetDocument(syntax.SyntaxTree);

            var semanticModel = document?.GetSemanticModelAsync().Result;
            if (semanticModel == null)
            {
                continue;
            }

            AnalyzeMethodBody(methodElement, syntax, semanticModel);
        }
    }

    /// <summary>
    ///     Adds "implements" relationships for interface members
    /// </summary>
    private void FindImplementationsForInterfaceMember(CodeElement element, ISymbol symbol)
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

            var implementingSymbol = FindImplementationForInterfaceMember(symbol, implementingType);
            if (implementingSymbol != null)
            {
                var implementingElement = _artifacts!.SymbolKeyToElementMap.GetValueOrDefault(implementingSymbol.Key());
                if (implementingElement != null)
                {
                    // Note: Implementations for external methods are not in our map
                    var locations = implementingSymbol.GetSymbolLocations();
                    AddRelationship(implementingElement, RelationshipType.Implements, element, locations, RelationshipAttribute.None);
                }
            }
            // That's ok, even interfaces are tested here
            //Trace.WriteLine(
            //    $"Implementing method {symbol.ContainingType?.Name}.{symbol.Name} not found for {implementingType.Name}");    
        }
    }

    /// <summary>
    ///     For methods and events.
    ///     Searches the whole hierarchy of the implementing type.
    ///     Returns the symbol (method for example) that is the first implementation of the interface member.
    ///     The later overrides are ignored here.
    /// </summary>
    private static ISymbol? FindImplementationForInterfaceMember(ISymbol symbol,
        INamedTypeSymbol implementingType)
    {
        var implementingSymbol = implementingType.FindImplementationForInterfaceMember(symbol);
        if (implementingSymbol is null)
        {
            // If the symbol is from a different compilation than the implementing type we may have to go deeper.
            var typeCompilation = implementingType.FindCompilation();
            var symbolCompilation = symbol.FindCompilation();
            if (!ReferenceEquals(typeCompilation, symbolCompilation))
            {
                if (symbol.FindCorrespondingSymbol(typeCompilation) is {} mappedSymbol)
                {
                    implementingSymbol = implementingType.FindImplementationForInterfaceMember(mappedSymbol);
                }
            }
        }

        return implementingSymbol;
    }

    /// <summary>
    ///     Returns the named types that directly implement the given interface.
    ///     Interfaces implemented in base types are not considered.
    /// </summary>
    private IEnumerable<INamedTypeSymbol> FindTypesImplementingInterface(INamedTypeSymbol interfaceSymbol)
    {
        // Note: AllInterfaces returns all interfaces found at this type, regardless if it is implemented in a base class or not.
        var interfaceKey = interfaceSymbol.Key();
        return _artifacts!.AllNamedTypesInSolution
            .Where(type => type.AllInterfaces.Any(i => i.Key() == interfaceKey));
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
        AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Overrides, locations, RelationshipAttribute.None);
    }

    private void AnalyzeFieldRelationships(CodeElement fieldElement, IFieldSymbol fieldSymbol)
    {
        // Get field declaration location
        var fieldLocations = fieldSymbol.GetSymbolLocations();
        var fieldLocation = fieldLocations.FirstOrDefault();

        AddTypeRelationship(fieldElement, fieldSymbol.Type, RelationshipType.Uses, fieldLocation);
    }

    /// <summary>
    ///     For method and property bodies.
    /// </summary>
    private void AnalyzeMethodBody(CodeElement sourceElement, SyntaxNode node, SemanticModel semanticModel)
    {
        foreach (var descendantNode in node.DescendantNodesAndSelf())
        {
            switch (descendantNode)
            {
                case ObjectCreationExpressionSyntax objectCreationSyntax:

                    // new SomeClass()
                    AnalyzeObjectCreation(sourceElement, semanticModel, objectCreationSyntax);
                    break;

                case InvocationExpressionSyntax invocationSyntax:

                    // Method()
                    AnalyzeInvocation(sourceElement, invocationSyntax, semanticModel);
                    break;

                case AssignmentExpressionSyntax assignmentExpression:
                    // Property and field assignments, event registration
                    AnalyzeAssignment(sourceElement, assignmentExpression, semanticModel);
                    break;

                case IdentifierNameSyntax identifierSyntax:
                    // Property or field access
                    AnalyzeIdentifier(sourceElement, identifierSyntax, semanticModel);
                    break;

                case MemberAccessExpressionSyntax memberAccessSyntax:

                    // obj.Property or obj.Field access
                    AnalyzeMemberAccess(sourceElement, memberAccessSyntax, semanticModel);
                    break;

                case ArgumentSyntax argumentSyntax:
                    AnalyzeArgument(sourceElement, argumentSyntax, semanticModel);
                    break;
            }
        }
    }

    private void AnalyzeObjectCreation(CodeElement sourceElement, SemanticModel semanticModel,
        ObjectCreationExpressionSyntax objectCreationSyntax)
    {
        var typeInfo = semanticModel.GetTypeInfo(objectCreationSyntax);
        if (typeInfo.Type != null)
        {
            var location = objectCreationSyntax.GetSyntaxLocation();
            AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Creates, location);
        }


        // Add calls relationship to constructor.
        // Only if explicitly declared, we don't want a fallback to the containing class.
        // I add this calls so that I can track method invocations.
        var symbolInfo = semanticModel.GetSymbolInfo(objectCreationSyntax);
        if (symbolInfo.Symbol is IMethodSymbol { IsImplicitlyDeclared: false } constructorSymbol)
        {
            var location = objectCreationSyntax.GetSyntaxLocation();

            // Normalize to original definition for generic types
            var normalizedConstructor = constructorSymbol;
            if (constructorSymbol.ContainingType.IsGenericType &&
                !constructorSymbol.ContainingType.IsDefinition)
            {
                // With generics Roslyn distinguishes between definition (written code) and usage (how code is used)
                // These are different symbols.
                normalizedConstructor = constructorSymbol.OriginalDefinition;
            }

            AddCallsRelationship(sourceElement, normalizedConstructor, location, RelationshipAttribute.None);
        }

        // Analyze constructor arguments for method groups
        if (objectCreationSyntax.ArgumentList != null)
        {
            foreach (var argument in objectCreationSyntax.ArgumentList.Arguments)
            {
                AnalyzeArgument(sourceElement, argument, semanticModel);
            }
        }
    }

    private void AnalyzeInvocation(CodeElement sourceElement, InvocationExpressionSyntax invocationSyntax,
        SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocationSyntax);
        if (symbolInfo.Symbol is IMethodSymbol calledMethod)
        {
            // Skip local functions - they should not be part of the dependency graph
            if (calledMethod.MethodKind == MethodKind.LocalFunction)
            {
                return;
            }

            var location = invocationSyntax.GetSyntaxLocation();

            var attributes = DetermineCallAttributes(invocationSyntax, calledMethod, semanticModel);
            AddCallsRelationship(sourceElement, calledMethod, location, attributes);


            // Handle generic method invocations
            if (calledMethod.IsGenericMethod)
            {
                foreach (var typeArg in calledMethod.TypeArguments)
                {
                    AddTypeRelationship(sourceElement, typeArg, RelationshipType.Uses, location);
                }
            }

            // Check if this is an event invocation using Invoke method
            if (calledMethod.Name == "Invoke")
            {
                IEventSymbol? eventSymbol = null;

                if (invocationSyntax.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    eventSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol as IEventSymbol;
                }
                else if (invocationSyntax.Expression is MemberBindingExpressionSyntax memberBinding)
                {
                    // Traverse up to find the ConditionalAccessExpressionSyntax
                    var currentNode = memberBinding.Parent;
                    while (currentNode != null && currentNode is not ConditionalAccessExpressionSyntax)
                    {
                        currentNode = currentNode.Parent;
                    }

                    if (currentNode is ConditionalAccessExpressionSyntax conditionalAccess)
                    {
                        eventSymbol = semanticModel.GetSymbolInfo(conditionalAccess.Expression).Symbol as IEventSymbol;
                    }
                }

                if (eventSymbol != null)
                {
                    AddEventInvocationRelationship(sourceElement, eventSymbol, location);
                }
            }
        }

        // Analyze method call arguments for method groups
        if (invocationSyntax.ArgumentList != null)
        {
            foreach (var argument in invocationSyntax.ArgumentList.Arguments)
            {
                AnalyzeArgument(sourceElement, argument, semanticModel);
            }
        }

        // Handle direct event invocations (if any)
        var invokedSymbol = semanticModel.GetSymbolInfo(invocationSyntax.Expression).Symbol;
        //if (invokedSymbol is IMethodSymbol { AssociatedSymbol: IEventSymbol symbol })
        if (invokedSymbol is IEventSymbol symbol)
        {
            AddEventInvocationRelationship(sourceElement, symbol, invocationSyntax.GetSyntaxLocation());
        }
    }

    private void AddEventInvocationRelationship(CodeElement sourceElement, IEventSymbol eventSymbol,
        SourceLocation location)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, RelationshipType.Invokes, [location], RelationshipAttribute.None);
    }

    private void AddEventUsageRelationship(CodeElement sourceElement, IEventSymbol eventSymbol, SourceLocation location, RelationshipAttribute attribute = RelationshipAttribute.None)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, RelationshipType.Uses, [location], attribute);
    }

    private void AnalyzeAssignment(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
        SemanticModel semanticModel)
    {
        // Analyze the left side of the assignment (target)
        AnalyzeExpressionForPropertyAccess(sourceElement, assignmentExpression.Left, semanticModel);

        // Analyze the right side of the assignment (value)
        AnalyzeExpressionForPropertyAccess(sourceElement, assignmentExpression.Right, semanticModel);

        var isRegistration = assignmentExpression.IsKind(SyntaxKind.AddAssignmentExpression);
        var isUnregistration = assignmentExpression.IsKind(SyntaxKind.SubtractAssignmentExpression);

        // Handle event registration and un-registration
        if (isRegistration || isUnregistration)
        {
            var leftSymbol = semanticModel.GetSymbolInfo(assignmentExpression.Left).Symbol;
            var rightSymbol = semanticModel.GetSymbolInfo(assignmentExpression.Right).Symbol;

            if (leftSymbol is IEventSymbol eventSymbol)
            {
                var attribute = isRegistration ? RelationshipAttribute.EventRegistration : RelationshipAttribute.EventUnregistration;
                AddEventUsageRelationship(sourceElement, eventSymbol, assignmentExpression.GetSyntaxLocation(), attribute);

                // If the right side is a method, add a Handles relationship
                if (rightSymbol is IMethodSymbol methodSymbol)
                {
                    // The handles relationship carries both locations for registering 
                    // and unregistering the event handler. We have the same with the uses relationship.
                    // But separately for registering and unregistering.
                    AddEventHandlerRelationship(methodSymbol, eventSymbol, assignmentExpression.GetSyntaxLocation(), attribute);
                }
            }
        }
    }

    private void AddEventHandlerRelationship(IMethodSymbol handlerMethod, IEventSymbol eventSymbol,
        SourceLocation location, RelationshipAttribute attribute)
    {
        var handlerElement = FindCodeElement(handlerMethod);
        var eventElement = FindCodeElement(eventSymbol);

        if (handlerElement != null && eventElement != null)
        {
            AddRelationship(handlerElement, RelationshipType.Handles, eventElement, [location], attribute);
        }
        //Trace.WriteLine(
        //    $"Unable to add 'Handles' relationship: Handler {handlerMethod.Name} or Event {eventSymbol.Name} not found in codebase.");
    }

    private void AnalyzeExpressionForPropertyAccess(CodeElement sourceElement, ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        switch (expression)
        {
            case IdentifierNameSyntax identifierSyntax:
                AnalyzeIdentifier(sourceElement, identifierSyntax, semanticModel);
                break;
            case MemberAccessExpressionSyntax memberAccessSyntax:
                AnalyzeMemberAccess(sourceElement, memberAccessSyntax, semanticModel);
                break;
            // Add more cases if needed for other types of expressions
        }
    }

    private void AddCallsRelationship(CodeElement sourceElement, IMethodSymbol methodSymbol, SourceLocation location, RelationshipAttribute attributes)
    {
        //Debug.Assert(FindCodeElement(methodSymbol)!= null);
        //Trace.WriteLine($"Adding call relationship: {sourceElement.Name} -> {methodSymbol.Name}");

        if (methodSymbol.IsExtensionMethod)
        {
            // Handle calls to extension methods
            methodSymbol = methodSymbol.ReducedFrom ?? methodSymbol;
        }

        if (methodSymbol.IsGenericMethod && FindCodeElement(methodSymbol) is null)
        {
            methodSymbol = methodSymbol.OriginalDefinition;
        }

        // If the method is not in our map, we might want to add a relationship to its containing type
        AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Calls, [location], attributes);
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
            AddTypeRelationship(element, typeSymbol.BaseType, RelationshipType.Inherits, typeLocation);
        }

        // Analyze implemented interfaces
        foreach (var @interface in typeSymbol.Interfaces)
        {
            AddTypeRelationship(element, @interface, RelationshipType.Implements, typeLocation);
        }
    }

    private void AddTypeRelationship(CodeElement sourceElement, ITypeSymbol typeSymbol,
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
                var symbolKey = typeSymbol.Key();
                if (_artifacts!.SymbolKeyToElementMap.TryGetValue(symbolKey, out var targetElement))
                {
                    AddRelationship(sourceElement, relationshipType, targetElement, location != null ? [location] : [], RelationshipAttribute.None);
                }

                break;
        }
    }


    private void AddRelationship(CodeElement source, RelationshipType type,
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

    private void AddNamedTypeRelationship(CodeElement sourceElement, INamedTypeSymbol namedTypeSymbol,
        RelationshipType relationshipType,
        SourceLocation? location)
    {
        var targetElement = FindCodeElement(namedTypeSymbol);
        if (targetElement != null)
        {
            // The type is internal (part of our codebase)
            AddRelationship(sourceElement, relationshipType, targetElement, location != null ? [location] : [], RelationshipAttribute.None);
        }
        else
        {
            // The type is external or a constructed generic type
            // Note the constructed type is not in our CodeElement map!
            // It is not found in phase1 the way we parse it but the original definition is.
            var originalDefinition = namedTypeSymbol.OriginalDefinition;
            var originalSymbolKey = originalDefinition.Key();

            if (_artifacts!.SymbolKeyToElementMap.TryGetValue(originalSymbolKey, out var originalTargetElement))
            {
                // We found the original definition, add relationship to it
                AddRelationship(sourceElement, relationshipType, originalTargetElement,
                    location != null ? [location] : [], RelationshipAttribute.None);
            }
            // The type is truly external, you might want to log this or handle it differently
            // AddExternalRelationship(sourceElement, namedTypeSymbol, relationshipType, location);
        }

        if (namedTypeSymbol.IsGenericType)
        {
            // Add "Uses" relationships to type arguments
            foreach (var typeArg in namedTypeSymbol.TypeArguments)
            {
                AddTypeRelationship(sourceElement, typeArg, RelationshipType.Uses, location);
            }
        }
    }

    private void AnalyzeIdentifier(CodeElement sourceElement, IdentifierNameSyntax identifierSyntax,
        SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(identifierSyntax);
        var symbol = symbolInfo.Symbol;

        if (identifierSyntax.Parent is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name == identifierSyntax)
        {
            // Skip if this identifier is part of a MemberAccessExpression. It will be handled by AnalyzeMemberAccess
            // Otherwise we get duplicated call relationships for the same line (different columns)
            return;
        }

        if (symbol is IPropertySymbol propertySymbol)
        {
            var location = identifierSyntax.GetSyntaxLocation();
            AddPropertyCallRelationship(sourceElement, propertySymbol, [location], RelationshipAttribute.None);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            var location = identifierSyntax.GetSyntaxLocation();
            AddRelationshipWithFallbackToContainingType(sourceElement, fieldSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
    }

    private void AnalyzeMemberAccess(CodeElement sourceElement, MemberAccessExpressionSyntax memberAccessSyntax,
        SemanticModel semanticModel)
    {
        // TODO Get information about the called type.
        // var typeInfo = semanticModel.GetTypeInfo(memberAccessSyntax.Expression);

        var symbolInfo = semanticModel.GetSymbolInfo(memberAccessSyntax);
        var symbol = symbolInfo.Symbol;

        if (symbol is IPropertySymbol propertySymbol)
        {
            var location = memberAccessSyntax.GetSyntaxLocation();
            AddPropertyCallRelationship(sourceElement, propertySymbol, [location], RelationshipAttribute.None);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            var location = memberAccessSyntax.GetSyntaxLocation();
            AddRelationshipWithFallbackToContainingType(sourceElement, fieldSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            // This handles cases where the event is accessed but not necessarily invoked
            AddEventUsageRelationship(sourceElement, eventSymbol, memberAccessSyntax.GetSyntaxLocation());
        }

        // Recursively analyze the expression in case of nested property access
        if (memberAccessSyntax.Expression is MemberAccessExpressionSyntax nestedMemberAccess)
        {
            AnalyzeMemberAccess(sourceElement, nestedMemberAccess, semanticModel);
        }
    }

    /// <summary>
    ///     Calling a property is treated like calling a method.
    /// </summary>
    private void AddPropertyCallRelationship(CodeElement sourceElement, IPropertySymbol propertySymbol,
        List<SourceLocation> locations, RelationshipAttribute attributes)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, propertySymbol, RelationshipType.Calls, locations, attributes);
    }

    private void AddRelationshipWithFallbackToContainingType(CodeElement sourceElement, ISymbol targetSymbol,
        RelationshipType relationshipType, List<SourceLocation>? locations, RelationshipAttribute attributes)
    {
        // If we don't have the property itself in our map, add a relationship to its containing type
        locations ??= [];

        var targetElement = FindCodeElement(targetSymbol);
        if (targetElement != null)
        {
            AddRelationship(sourceElement, relationshipType, targetElement, locations, attributes);
            return;
        }

        var containingTypeElement = FindCodeElement(targetSymbol.ContainingType);
        if (containingTypeElement != null)
        {
            AddRelationship(sourceElement, relationshipType, containingTypeElement, locations, attributes);
        }
    }

    private CodeElement? FindCodeElement(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        _artifacts!.SymbolKeyToElementMap.TryGetValue(symbol.Key(), out var element);
        return element;
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
        AddTypeRelationship(propertyElement, propertySymbol.Type, RelationshipType.Uses, propertyLocation);

        if (propertySymbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            FindImplementationsForInterfaceMember(propertyElement, propertySymbol);
        }

        // Check for property override
        if (propertySymbol.IsOverride)
        {
            var overriddenProperty = propertySymbol.OverriddenProperty;
            if (overriddenProperty != null)
            {
                var locations = propertySymbol.GetSymbolLocations();
                AddPropertyRelationship(propertyElement, overriddenProperty, RelationshipType.Overrides, locations);
            }
        }

        // Analyze the property body (including accessors)
        AnalyzePropertyBody(solution, propertyElement, propertySymbol);
    }

    private void AnalyzePropertyBody(Solution solution, CodeElement propertyElement, IPropertySymbol propertySymbol)
    {
        foreach (var syntaxReference in propertySymbol.DeclaringSyntaxReferences)
        {
            var syntax = syntaxReference.GetSyntax();
            if (syntax is PropertyDeclarationSyntax propertyDeclaration)
            {
                var document = solution.GetDocument(syntax.SyntaxTree);
                var semanticModel = document?.GetSemanticModelAsync().Result;
                if (semanticModel != null)
                {
                    if (propertyDeclaration.ExpressionBody != null)
                    {
                        AnalyzeMethodBody(propertyElement, propertyDeclaration.ExpressionBody.Expression,
                            semanticModel);
                    }
                    else if (propertyDeclaration.AccessorList != null)
                    {
                        foreach (var accessor in propertyDeclaration.AccessorList.Accessors)
                        {
                            if (accessor.ExpressionBody != null)
                            {
                                AnalyzeMethodBody(propertyElement, accessor.ExpressionBody.Expression, semanticModel);
                            }
                            else if (accessor.Body != null)
                            {
                                AnalyzeMethodBody(propertyElement, accessor.Body, semanticModel);
                            }
                        }
                    }
                }
            }
        }
    }

    private void AddPropertyRelationship(CodeElement sourceElement, IPropertySymbol propertySymbol,
        RelationshipType relationshipType, List<SourceLocation> locations)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, propertySymbol, relationshipType, locations, RelationshipAttribute.None);
    }




    private RelationshipAttribute DetermineCallAttributes(InvocationExpressionSyntax invocation,
        IMethodSymbol method, SemanticModel semanticModel)
    {
        if (method.IsExtensionMethod)
        {
            return RelationshipAttribute.IsExtensionMethodCall;
        }

        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                return AnalyzeMemberAccessCallType(memberAccess, method, semanticModel);

            case IdentifierNameSyntax:
                // Direct method call - could be this.Method() or static
                return method.IsStatic ? RelationshipAttribute.IsStaticCall : RelationshipAttribute.None;

            case MemberBindingExpressionSyntax:
                // Conditional access: obj?.Method()
                return RelationshipAttribute.IsInstanceCall;

            default:
                // Fallback for complex expressions
                return RelationshipAttribute.None;
        }
    }

    private static RelationshipAttribute AnalyzeMemberAccessCallType(MemberAccessExpressionSyntax memberAccess,
        IMethodSymbol method, SemanticModel semanticModel)
    {
        switch (memberAccess.Expression)
        {
            case BaseExpressionSyntax:
                // base.Method()
                return RelationshipAttribute.IsBaseCall;

            case ThisExpressionSyntax:
                // this.Method()
                return RelationshipAttribute.IsThisCall;

            case IdentifierNameSyntax identifier:
                var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                if (symbolInfo.Symbol is INamedTypeSymbol)
                {
                    // Type.StaticMethod()
                    return RelationshipAttribute.IsStaticCall;
                }

                if (symbolInfo.Symbol is IFieldSymbol || symbolInfo.Symbol is IPropertySymbol)
                {
                    // field.Method() or property.Method()
                    return RelationshipAttribute.IsInstanceCall;
                }

                // Local variable or parameter
                return RelationshipAttribute.IsInstanceCall;

            case MemberAccessExpressionSyntax:
                // Chained calls: obj.Property.Method()
                return RelationshipAttribute.IsInstanceCall;

            case InvocationExpressionSyntax:
                // Method call result: GetObject().Method()
                return RelationshipAttribute.IsInstanceCall;

            case ObjectCreationExpressionSyntax:
                // new Object().Method()
                return RelationshipAttribute.IsInstanceCall;

            default:
                // Complex expression - default to instance call
                return RelationshipAttribute.IsInstanceCall;
        }
    }

    private void AnalyzeArgument(CodeElement sourceElement, ArgumentSyntax argumentSyntax, SemanticModel semanticModel)
    {
        var expression = argumentSyntax.Expression;

        // Handle method groups passed as arguments
        if (expression is IdentifierNameSyntax identifierSyntax)
        {
            // Foo(MethodGroup)
            var symbolInfo = semanticModel.GetSymbolInfo(identifierSyntax);
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                // Skip local functions - they should not be part of the dependency graph
                if (methodSymbol.MethodKind == MethodKind.LocalFunction)
                {
                    return;
                }

                // This is a method group reference
                var location = identifierSyntax.GetSyntaxLocation();

                //AddCallsRelationship(sourceElement, methodSymbol, location, RelationshipAttribute.IsMethodGroup);
                AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Uses, [location], RelationshipAttribute.IsMethodGroup);
            }
        }
        else if (expression is MemberAccessExpressionSyntax memberAccessSyntax)
        {
            // obj.MethodGroup
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccessSyntax);
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                // Skip local functions - they should not be part of the dependency graph
                if (methodSymbol.MethodKind == MethodKind.LocalFunction)
                {
                    return;
                }

                // This is a method group reference like obj.Method
                var location = memberAccessSyntax.GetSyntaxLocation();

                // AddCallsRelationship(sourceElement, methodSymbol, location, RelationshipAttribute.IsMethodGroup);
                AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Uses, [location], RelationshipAttribute.IsMethodGroup);
            }
        }
    }
}