using System.Diagnostics;
using CodeParser.Parser.Config;
using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Phase 2/2 of the parser: Analyzing relationships between code elements.
/// </summary>
public class RelationshipAnalyzer : ISyntaxNodeHandler
{
    private readonly ParserConfig _config;

    private readonly ExternalCodeElementCache _externalCodeElementCache = new();
    private readonly object _lock = new();
    private readonly Progress _progress;
    private Artifacts? _artifacts;
    private CodeGraph? _codeGraph;
    private long _lastProgress;

    private int _processedCodeElements;

    /// <summary>
    ///     Phase 2/2 of the parser: Analyzing relationships between code elements.
    /// </summary>
    public RelationshipAnalyzer(Progress progress, ParserConfig config)
    {
        _progress = progress;
        _config = config;
    }


    public void AnalyzeInvocation(CodeElement sourceElement, InvocationExpressionSyntax invocationSyntax,
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

        // Note: Arguments are now handled by the MethodBodyWalker.VisitArgument

        // Handle direct event invocations (if any)
        var invokedSymbol = semanticModel.GetSymbolInfo(invocationSyntax.Expression).Symbol;
        //if (invokedSymbol is IMethodSymbol { AssociatedSymbol: IEventSymbol symbol })
        if (invokedSymbol is IEventSymbol symbol)
        {
            AddEventInvocationRelationship(sourceElement, symbol, invocationSyntax.GetSyntaxLocation());
        }
    }

    public void AnalyzeAssignment(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
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
                    // and unregistering the event handler. We have the same with the "uses" relationship.
                    // But separately for registering and unregistering.
                    AddEventHandlerRelationship(methodSymbol, eventSymbol, assignmentExpression.GetSyntaxLocation(), attribute);
                }
            }
        }
    }

    /// <summary>
    ///     Public wrapper for AddTypeRelationship to allow access from LambdaBodyWalker
    /// </summary>
    public void AddTypeRelationshipPublic(CodeElement sourceElement, ITypeSymbol typeSymbol,
        RelationshipType relationshipType,
        SourceLocation? location = null)
    {
        AddTypeRelationship(sourceElement, typeSymbol, relationshipType, location);
    }

    /// <summary>
    ///     Public wrapper for AddRelationshipWithFallbackToContainingType to allow access from LambdaBodyWalker.
    ///     Adds a relationship to a symbol (method, property, field, event), with fallback to containing type for external symbols.
    /// </summary>
    public void AddSymbolRelationshipPublic(CodeElement sourceElement, ISymbol targetSymbol,
        RelationshipType relationshipType, List<SourceLocation>? locations, RelationshipAttribute attributes)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, targetSymbol, relationshipType, locations, attributes);
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeIdentifier" />
    /// </summary>
    public void AnalyzeIdentifier(CodeElement sourceElement, IdentifierNameSyntax identifierSyntax,
        SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(identifierSyntax);
        var symbol = symbolInfo.Symbol;

        // No guard needed - the walker ensures we only visit standalone identifiers
        // MemberAccess expressions handle their own identifiers explicitly

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

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeMemberAccess" />
    /// </summary>
    public void AnalyzeMemberAccess(CodeElement sourceElement, MemberAccessExpressionSyntax memberAccessSyntax,
        SemanticModel semanticModel)
    {
        // Analyze the member being accessed (the right side of the dot)
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

        // Note: We don't recursively handle the Expression here.
        // The walker's Visit(node.Expression) call handles nested member access automatically.
    }

    public void AnalyzeArgument(CodeElement sourceElement, ArgumentSyntax argumentSyntax, SemanticModel semanticModel)
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

    public void AnalyzeLocalDeclaration(CodeElement sourceElement, LocalDeclarationStatementSyntax localDeclaration,
        SemanticModel semanticModel)
    {
        // Get the type of the local variable declaration
        var typeInfo = semanticModel.GetTypeInfo(localDeclaration.Declaration.Type);
        if (typeInfo.Type != null)
        {
            var location = localDeclaration.Declaration.Type.GetSyntaxLocation();
            AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeObjectCreation" />
    /// </summary>
    public void AnalyzeObjectCreation(CodeElement sourceElement, SemanticModel semanticModel,
        BaseObjectCreationExpressionSyntax objectCreationSyntax, bool isFieldInitializer)
    {
        var typeInfo = semanticModel.GetTypeInfo(objectCreationSyntax);
        if (typeInfo.Type != null)
        {
            var location = objectCreationSyntax.GetSyntaxLocation();

            if (isFieldInitializer)
            {
                // Field "uses" the created class
                AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Uses, location);

                // Containing class "creates" the object
                if (sourceElement.Parent != null)
                {
                    AddTypeRelationship(sourceElement.Parent, typeInfo.Type, RelationshipType.Creates, location);
                }
            }
            else
            {
                // Method "creates" the object
                AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Creates, location);
            }
        }

        // When handling field initializers don't add calls relationship to ctor. 
        if (!isFieldInitializer)
        {
            // Add "calls" relationship to constructor. Primary, implicit and external constructors are ignored.
            // (!) We do not want a fallback to the containing class here (!) We still have the "creates" relationship.
            // Adding this relationship allows following method invocations later.
            var symbolInfo = semanticModel.GetSymbolInfo(objectCreationSyntax);
            if (symbolInfo.Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor, IsImplicitlyDeclared: false } constructorSymbol)
            {
                // Constructors are never generic in C#. We use the symbol of the definition found in phase 1
                // So IsGeneric is never true, yet we need the original definition.
                var normalizedConstructor = (IMethodSymbol)constructorSymbol.NormalizeToOriginalDefinition();
                if (normalizedConstructor.IsExplicitConstructor() && FindInternalCodeElement(normalizedConstructor) is not null)
                {
                    var location = objectCreationSyntax.GetSyntaxLocation();
                    AddCallsRelationship(sourceElement, normalizedConstructor, location, RelationshipAttribute.None);
                }
            }
        }

        // Note: Arguments are now handled by the MethodBodyWalker.VisitArgument
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

        // Take a snapshot of internal elements to avoid collection modification during parallel iteration
        var internalElements = _codeGraph.Nodes.Values.ToList();

        Parallel.ForEach(internalElements, AnalyzeRelationshipsLocal);

        // After parallel processing, add all external elements to the graph
        AddExternalElementsToGraph();

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

    /// <summary>
    ///     Adds all external elements that were created during parallel processing to the code graph.
    ///     This must be called after parallel processing completes to avoid collection modification issues.
    /// </summary>
    private void AddExternalElementsToGraph()
    {
        if (!_config.IncludeExternals)
        {
            return;
        }

        foreach (var externalElement in _externalCodeElementCache.GetCodeElements())
        {
            _codeGraph!.Nodes[externalElement.Id] = externalElement;
        }
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

        // Take a snapshot to avoid collection modification during iteration
        var internalElements = _codeGraph.Nodes.Values.ToList();

        foreach (var element in internalElements)
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

        // Add external elements to the graph
        AddExternalElementsToGraph();

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
            AnalyzeFieldRelationships(solution, element, fieldSymbol);
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

    private void AnalyzeFieldRelationships(Solution solution, CodeElement fieldElement, IFieldSymbol fieldSymbol)
    {
        // Get field declaration location
        var fieldLocations = fieldSymbol.GetSymbolLocations();
        var fieldLocation = fieldLocations.FirstOrDefault();

        AddTypeRelationship(fieldElement, fieldSymbol.Type, RelationshipType.Uses, fieldLocation);


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
                    AnalyzeMethodBody(fieldElement, variableDeclarator.Initializer.Value, semanticModel, true);
                }
            }
        }
    }

    /// <summary>
    ///     For method and property bodies and field initializers.
    /// </summary>
    public void AnalyzeMethodBody(CodeElement sourceElement, SyntaxNode node, SemanticModel semanticModel, bool isFieldInitializer = false)
    {
        var walker = new MethodBodyWalker(this, sourceElement, semanticModel, isFieldInitializer);
        walker.Visit(node);
    }

    public void AddEventInvocationRelationship(CodeElement sourceElement, IEventSymbol eventSymbol,
        SourceLocation location)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, RelationshipType.Invokes, [location], RelationshipAttribute.None);
    }

    private void AddEventUsageRelationship(CodeElement sourceElement, IEventSymbol eventSymbol, SourceLocation location, RelationshipAttribute attribute = RelationshipAttribute.None)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, RelationshipType.Uses, [location], attribute);
    }

    private void AddEventHandlerRelationship(IMethodSymbol handlerMethod, IEventSymbol eventSymbol,
        SourceLocation location, RelationshipAttribute attribute)
    {
        var handlerElement = FindInternalCodeElement(handlerMethod);
        var eventElement = FindInternalCodeElement(eventSymbol);

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

        if (methodSymbol.IsGenericMethod && FindInternalCodeElement(methodSymbol) is null)
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

                DebugRelationship(source, target, type);
                source.Relationships.Add(newRelationship);
            }
        }
    }

    private void DebugRelationship(CodeElement source, CodeElement target, RelationshipType type)
    {
        // if (source.Name == "Run" && target.Name == "IServiceC") Debugger.Break();
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
        var normalizedSymbol = namedTypeSymbol is { IsGenericType: true, IsDefinition: false }
            ? namedTypeSymbol.OriginalDefinition
            : namedTypeSymbol;

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
    ///     Calling a property is treated like calling a method.
    /// </summary>
    private void AddPropertyCallRelationship(CodeElement sourceElement, IPropertySymbol propertySymbol,
        List<SourceLocation> locations, RelationshipAttribute attributes)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, propertySymbol, RelationshipType.Calls, locations, attributes);
    }

    /// <summary>
    ///     Adds a relationship to a symbol, with configurable fallback behavior for external symbols.
    ///     Current behavior: For external symbols, creates relationships to the CONTAINING TYPE only.
    ///     Example: myList.Add(5) -> relationship to List&lt;T&gt; (not to List&lt;T&gt;.Add)
    /// 
    ///     TO CHANGE TO METHOD-LEVEL EXTERNAL RELATIONSHIPS:
    ///     Change line marked with "FALLBACK BEHAVIOR" below from:
    ///     GetOrCreateCodeElement(targetSymbol.ContainingType)
    ///     To:
    ///     GetOrCreateCodeElement(targetSymbol)
    ///     This will create external method/property/field elements instead of just type-level relationships.
    /// </summary>
    private void AddRelationshipWithFallbackToContainingType(CodeElement sourceElement, ISymbol targetSymbol,
        RelationshipType relationshipType, List<SourceLocation>? locations, RelationshipAttribute attributes)
    {
        locations ??= [];

        // Try to find the symbol as-is first (covers any edge cases with generics)
        var targetElement = FindInternalCodeElement(targetSymbol);
        if (targetElement != null)
        {
            AddRelationship(sourceElement, relationshipType, targetElement, locations, attributes);
            return;
        }


        // If not found, and it's a constructed type/member, normalize and try again
        var normalizedSymbol = targetSymbol.NormalizeToOriginalDefinition();
        if (!SymbolEqualityComparer.Default.Equals(normalizedSymbol, targetSymbol))
        {
            targetElement = FindInternalCodeElement(normalizedSymbol);
            if (targetElement != null)
            {
                AddRelationship(sourceElement, relationshipType, targetElement, locations, attributes);
                return;
            }
        }

        // Not found internally - try containing type
        var containingTypeElement = FindInternalCodeElement(targetSymbol.ContainingType);
        if (containingTypeElement != null)
        {
            // Containing type found internally, use it
            // Examples: containing type may be
            // - Enum when an enum value is referenced.
            // - Class or Record when a primary constructor initialized property is accessed (member access)
            AddRelationship(sourceElement, relationshipType, containingTypeElement, locations, attributes);
            return;
        }

        // External handling
        if (_config.IncludeExternals)
        {
            // FALLBACK BEHAVIOR: Currently creates relationship to types only.
            // I also map all relationships to "Uses". If you want method level consider also that you find Enum values etc.

            INamedTypeSymbol? externalType = null;

            // If target is already a type use it.
            if (targetSymbol is INamedTypeSymbol namedType)
            {
                externalType = namedType;
            }
            // Otherwise use ContainingType (for Methods, Properties, Fields, etc.)
            else if (targetSymbol.ContainingType != null)
            {
                externalType = targetSymbol.ContainingType;
            }

            if (externalType != null)
            {
                // Normalize to OriginalDefinition (z.B. List<int> → List<T>)
                externalType = (INamedTypeSymbol)externalType.NormalizeToOriginalDefinition();

                var externalElement = TryGetOrCreateExternalCodeElement(externalType);
                if (externalElement is not null)
                {
                    AddRelationship(sourceElement, RelationshipType.Uses, externalElement, locations, attributes);
                }
            }
        }
    }

    /// <summary>
    ///     The caller has to take care that the symbol is normalized to original definition if necessary
    /// </summary>
    private CodeElement? FindInternalCodeElement(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        // If not called here I muss 3 calls why?
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
                return AnalyzeMemberAccessCallType(memberAccess, semanticModel);

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

    private static RelationshipAttribute AnalyzeMemberAccessCallType(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
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
}