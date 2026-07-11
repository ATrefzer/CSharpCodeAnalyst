using System.Diagnostics;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Phase 2/2 of the parser: Analyzing relationships between code elements.
/// </summary>
public class RelationshipAnalyzer : ISyntaxNodeHandler
{
    private readonly ParserConfig _config;

    private readonly ExternalCodeElementCache _externalCodeElementCache = new();
    private readonly Lock _lock = new();
    private readonly IProgress<string>? _progress;
    private Artifacts? _artifacts;
    private CodeGraph.Graph.CodeGraph? _codeGraph;
    private long _lastProgress;

    private int _processedCodeElements;

    /// <summary>
    ///     Phase 2/2 of the parser: Analyzing relationships between code elements.
    /// </summary>
    public RelationshipAnalyzer(IProgress<string>? progress, ParserConfig config)
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

        // Note: Arguments (including method groups passed as arguments) are handled by the walker's
        // normal traversal into the argument expressions (AnalyzeIdentifier / AnalyzeMemberAccess).

        // Handle direct event invocations (if any)
        var invokedSymbol = semanticModel.GetSymbolInfo(invocationSyntax.Expression).Symbol;
        //if (invokedSymbol is IMethodSymbol { AssociatedSymbol: IEventSymbol symbol })
        if (invokedSymbol is IEventSymbol symbol)
        {
            AddEventInvocationRelationship(sourceElement, symbol, invocationSyntax.GetSyntaxLocation());
        }
    }

    public void AnalyzeEventRegistrationAssignment(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
        SemanticModel semanticModel)
    {
        // Note: Property/field access on left and right sides is handled by the walker's normal traversal
        // (VisitIdentifierName and VisitMemberAccessExpression). We only need to handle event registration/unregistration here.

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

                //                                                                   =>  Extract single argument from the list.
                if (assignmentExpression.Right is BaseObjectCreationExpressionSyntax { ArgumentList.Arguments: [var handlerArgument] })
                {
                    // Old style: event += new SomeDelegate(Handler). GetSymbolInfo on a delegate
                    // creation yields no symbol, so we recognize it by syntax and take the handler
                    // from the single constructor argument. (A non-method argument, e.g. an existing
                    // delegate instance, simply yields no Handles relationship.)
                    if (semanticModel.GetSymbolInfo(handlerArgument.Expression).Symbol is IMethodSymbol handlerFromCtor)
                    {
                        AddEventHandlerRelationship(handlerFromCtor, eventSymbol, assignmentExpression.GetSyntaxLocation(), attribute);
                    }
                }
                else if (rightSymbol is IMethodSymbol methodSymbol)
                {
                    // Modern style: event += Handler. The right side is the method itself.
                    // The handles relationship carries both locations for registering
                    // and unregistering the event handler. We have the same with the "uses" relationship.
                    // But separately for registering and unregistering.
                    AddEventHandlerRelationship(methodSymbol, eventSymbol, assignmentExpression.GetSyntaxLocation(), attribute);
                }
            }
        }
    }

    public void AnalyzeConstructorInitializer(CodeElement sourceElement, ConstructorInitializerSyntax initializerSyntax,
        SemanticModel semanticModel)
    {
        // ": base(...)" / ": this(...)". The arguments are visited separately by the walker.
        // We mirror the constructor handling in AnalyzeObjectCreation: only link explicit, internal
        // constructors. Implicit base constructors and external ones are left to the Inherits edge.
        if (semanticModel.GetSymbolInfo(initializerSyntax).Symbol is
            IMethodSymbol { MethodKind: MethodKind.Constructor, IsImplicitlyDeclared: false } constructorSymbol)
        {
            var normalizedConstructor = constructorSymbol.NormalizeToOriginalDefinition();
            if (normalizedConstructor.IsExplicitConstructor() && FindInternalCodeElement(normalizedConstructor) is not null)
            {
                var attribute = initializerSyntax.IsKind(SyntaxKind.BaseConstructorInitializer)
                    ? RelationshipAttribute.IsBaseCall
                    : RelationshipAttribute.IsThisCall;
                AddCallsRelationship(sourceElement, normalizedConstructor, initializerSyntax.GetSyntaxLocation(), attribute);
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
    ///     Adds a relationship to a symbol (method, property, field, event), with fallback to containing type for external
    ///     symbols.
    /// </summary>
    public void AddSymbolRelationshipPublic(CodeElement sourceElement, ISymbol targetSymbol,
        RelationshipType relationshipType, List<SourceLocation>? locations, RelationshipAttribute attributes)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, targetSymbol, relationshipType, locations, attributes);
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AddConstructorReferenceFromLambda" />
    ///     Mirrors the constructor handling in <see cref="AnalyzeObjectCreation" /> (same guard: explicit,
    ///     internal constructors only) but records a "Uses" instead of a "Calls" relationship, because a
    ///     lambda only references the constructor. Implicit/primary/external constructors carry no edge here;
    ///     the type "Uses" the lambda walker already adds covers them.
    /// </summary>
    public void AddConstructorReferenceFromLambda(CodeElement sourceElement,
        BaseObjectCreationExpressionSyntax objectCreationSyntax, SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(objectCreationSyntax).Symbol is
            IMethodSymbol { MethodKind: MethodKind.Constructor, IsImplicitlyDeclared: false } constructorSymbol)
        {
            var normalizedConstructor = constructorSymbol.NormalizeToOriginalDefinition();
            if (normalizedConstructor.IsExplicitConstructor() && FindInternalCodeElement(normalizedConstructor) is not null)
            {
                var location = objectCreationSyntax.GetSyntaxLocation();
                AddRelationshipWithFallbackToContainingType(sourceElement, normalizedConstructor,
                    RelationshipType.Uses, [location], RelationshipAttribute.None);
            }
        }
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeTypeSyntax" />
    /// </summary>
    public void AnalyzeTypeSyntax(CodeElement sourceElement, SemanticModel semanticModel, TypeSyntax? node)
    {
        if (node is null)
        {
            return;
        }

        // typeof(Foo) creates a "Uses" relationship to the type
        var typeInfo = semanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            var location = node.GetSyntaxLocation();
            AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeIdentifier" />
    /// </summary>
    public void AnalyzeIdentifier(CodeElement sourceElement, IdentifierNameSyntax identifierSyntax,
        SemanticModel semanticModel, RelationshipType propertyAccessType = RelationshipType.Calls)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(identifierSyntax);
        var symbol = symbolInfo.Symbol;

        // No guard needed - the walker ensures we only visit standalone identifiers
        // MemberAccess expressions handle their own identifiers explicitly

        if (symbol is IPropertySymbol propertySymbol)
        {
            var location = identifierSyntax.GetSyntaxLocation();
            AddPropertyAccessRelationship(sourceElement, propertySymbol, identifierSyntax, propertyAccessType, location, semanticModel);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            var location = identifierSyntax.GetSyntaxLocation();
            AddRelationshipWithFallbackToContainingType(sourceElement, fieldSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            var location = identifierSyntax.GetSyntaxLocation();
            AddEventUsageRelationship(sourceElement, eventSymbol, location);
        }
        else if (symbol is IMethodSymbol methodSymbol && IsMethodGroupReference(identifierSyntax, methodSymbol))
        {
            // Foo passed/assigned/returned as a delegate (method group), not invoked.
            var location = identifierSyntax.GetSyntaxLocation();
            AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Uses, [location], RelationshipAttribute.IsMethodGroup);
        }
    }

    /// <summary>
    ///     A method name is a method group reference (Action a = Foo; return Foo; _field = Foo)
    ///     unless it is the expression being invoked (Foo()) - that is a call handled by
    ///     AnalyzeInvocation.
    ///     Local functions are never part of the graph. Constructors are never method groups: an
    ///     attribute usage ([Foo]) binds its name to the attribute constructor, and the walker also
    ///     visits the attribute lists of a declaration - we must not turn that into a Uses edge.
    /// </summary>
    private static bool IsMethodGroupReference(ExpressionSyntax node, IMethodSymbol methodSymbol)
    {
        if (methodSymbol.MethodKind is MethodKind.LocalFunction or MethodKind.Constructor)
        {
            return false;
        }

        // Determine the expression that would be the invocation target if this were a call:
        //   Method() / obj.Method()  -> the node itself
        //   obj?.Method()            -> the enclosing member-binding expression (the identifier's
        //                               direct parent is the MemberBindingExpression, not the call)
        var callTarget = node.Parent is MemberBindingExpressionSyntax binding ? (ExpressionSyntax)binding : node;

        return callTarget.Parent is not InvocationExpressionSyntax invocation ||
               !ReferenceEquals(invocation.Expression, callTarget);
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeMemberAccess" />
    /// </summary>
    public void AnalyzeMemberAccess(CodeElement sourceElement, MemberAccessExpressionSyntax memberAccessSyntax,
        SemanticModel semanticModel, RelationshipType propertyAccessType = RelationshipType.Calls)
    {
        // Analyze the member being accessed (the right side of the dot)
        var symbolInfo = semanticModel.GetSymbolInfo(memberAccessSyntax);
        var symbol = symbolInfo.Symbol;

        if (symbol is IPropertySymbol propertySymbol)
        {
            var location = memberAccessSyntax.GetSyntaxLocation();
            AddPropertyAccessRelationship(sourceElement, propertySymbol, memberAccessSyntax, propertyAccessType, location, semanticModel);
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
        else if (symbol is IMethodSymbol methodSymbol && IsMethodGroupReference(memberAccessSyntax, methodSymbol))
        {
            // obj.Foo passed/assigned/returned as a delegate (method group), not invoked.
            var location = memberAccessSyntax.GetSyntaxLocation();
            AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Uses, [location], RelationshipAttribute.IsMethodGroup);
        }

        // Note: We don't recursively handle the Expression here.
        // The walker's Visit(node.Expression) call handles nested member access automatically.
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeElementAccess" />
    ///     An indexer access is a property access spelled with brackets, so it runs through the same
    ///     routing as AnalyzeIdentifier / AnalyzeMemberAccess: read/write classification, get/set accessor
    ///     split and the fallback to the containing type for external indexers.
    /// </summary>
    public void AnalyzeElementAccess(CodeElement sourceElement, ExpressionSyntax elementAccessSyntax,
        SemanticModel semanticModel, RelationshipType propertyAccessType = RelationshipType.Calls)
    {
        // Array element access ("_data[i]") resolves to no symbol; only user-defined indexers matter here.
        if (semanticModel.GetSymbolInfo(elementAccessSyntax).Symbol is IPropertySymbol { IsIndexer: true } indexerSymbol)
        {
            var location = elementAccessSyntax.GetSyntaxLocation();
            AddPropertyAccessRelationship(sourceElement, indexerSymbol, elementAccessSyntax, propertyAccessType, location, semanticModel);
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
                var normalizedConstructor = constructorSymbol.NormalizeToOriginalDefinition();
                if (normalizedConstructor.IsExplicitConstructor() && FindInternalCodeElement(normalizedConstructor) is not null)
                {
                    var location = objectCreationSyntax.GetSyntaxLocation();
                    AddCallsRelationship(sourceElement, normalizedConstructor, location, RelationshipAttribute.None);
                }
            }
        }

        // Note: Arguments (including method groups passed as arguments) are handled by the walker's
        // normal traversal into the argument expressions (AnalyzeIdentifier / AnalyzeMemberAccess).
    }

    /// <summary>
    ///     Builds all relationships (phase 2). The code graph is updated, the artifacts are read only.
    ///     Pass <paramref name="maxDegreeOfParallelism" /> = 1 for a deterministic single-threaded run
    ///     (useful when debugging); the default (-1) lets the scheduler use all available cores.
    /// </summary>
    public Task AnalyzeRelationships(Solution solution, CodeGraph.Graph.CodeGraph codeGraph, Artifacts artifacts,
        int maxDegreeOfParallelism = -1)
    {
        ArgumentNullException.ThrowIfNull(solution, nameof(solution));
        ArgumentNullException.ThrowIfNull(codeGraph, nameof(codeGraph));
        ArgumentNullException.ThrowIfNull(artifacts, nameof(artifacts));


        _codeGraph = codeGraph;
        _artifacts = artifacts;

        var numberOfCodeElements = _codeGraph.Nodes.Count;
        _processedCodeElements = 0;

        // Take a snapshot of internal elements to avoid collection modification during iteration
        var internalElements = _codeGraph.Nodes.Values.ToList();

        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };
        Parallel.ForEach(internalElements, options, AnalyzeRelationshipsLocal);

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

    private void SendParserPhase2Progress(int processed, int total)
    {
        var currentProgress = (long)Math.Floor(processed / (double)total * 100);
        var lastReported = Interlocked.Read(ref _lastProgress);

        if (currentProgress > lastReported)
        {
            if (Interlocked.CompareExchange(ref _lastProgress, currentProgress, lastReported) == lastReported)
            {
                var msg = $"Phase 2/2: Analyzing relationships. Finished {currentProgress}%.";
                _progress?.Report(msg);
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
            var dummyMethodFullName = $"{dummyClassFullName}.{dummyMethodName}";
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
            AddTypeRelationship(methodElement, parameter.Type, RelationshipType.Uses, methodLocation);
        }

        // Analyze generic type-parameter constraints (where T : Foo)
        AnalyzeTypeParameterConstraints(methodElement, methodSymbol.TypeParameters, methodLocation);

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
        // The interface-key -> implementing-types map is precomputed in phase 1 (see Artifacts). It already
        // accounts for interfaces implemented in a base type, since it is built from AllInterfaces.
        var interfaceKey = interfaceSymbol.Key();
        return _artifacts!.InterfaceImplementations.GetValueOrDefault(interfaceKey) ?? [];
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

    private void AddCallsRelationship(CodeElement sourceElement, IMethodSymbol methodSymbol, SourceLocation location, RelationshipAttribute attributes)
    {
        //Debug.Assert(FindCodeElement(methodSymbol)!= null);
        //Trace.WriteLine($"Adding call relationship: {sourceElement.Name} -> {methodSymbol.Name}");

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

        // Analyze generic type-parameter constraints (where T : Foo)
        AnalyzeTypeParameterConstraints(element, typeSymbol.TypeParameters, typeLocation);

        AnalyzePrimaryConstructorParameters(element, typeSymbol);
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
                AddTypeRelationship(element, constraintType, RelationshipType.Uses, location);
            }
        }
    }

    /// <summary>
    ///     Phase 1 only collects ConstructorDeclarationSyntax, so primary constructors (including the
    ///     positional parameters of records) have no method element and the parameter types would
    ///     otherwise create no relationship. A primary constructor is recognized by its declaring
    ///     syntax being the TypeDeclarationSyntax itself (same detection as IsExplicitConstructor).
    /// </summary>
    private void AnalyzePrimaryConstructorParameters(CodeElement element, INamedTypeSymbol typeSymbol)
    {
        foreach (var constructor in typeSymbol.InstanceConstructors)
        {
            // Synthesized constructors (the record copy constructor, default constructors) are
            // implicitly declared and carry no user-written parameter types we care about.
            if (constructor.IsImplicitlyDeclared)
            {
                continue;
            }

            // A primary constructor has no ConstructorDeclarationSyntax of its own; its declaring
            // syntax is the type declaration itself (same detection as IsExplicitConstructor).
            var isPrimary = constructor.DeclaringSyntaxReferences
                .Any(r => r.GetSyntax() is TypeDeclarationSyntax);
            if (!isPrimary)
            {
                continue;
            }

            foreach (var parameter in constructor.Parameters)
            {
                var location = parameter.GetSymbolLocations().FirstOrDefault();
                AddTypeRelationship(element, parameter.Type, RelationshipType.Uses, location);
            }
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
    ///     Routes a property access to the correct accessor element when splitting is enabled.
    ///     A read access targets the getter, a write access the setter, and a read-modify-write access
    ///     (<c>+=</c>, <c>++</c>, ...) both. If splitting is disabled, or the property is external (no
    ///     accessor elements exist), the access falls back to a relationship to the property itself.
    /// </summary>
    private void AddPropertyAccessRelationship(CodeElement sourceElement, IPropertySymbol propertySymbol,
        ExpressionSyntax accessExpression, RelationshipType relationshipType, SourceLocation location,
        SemanticModel semanticModel)
    {
        // A property referenced inside nameof(...) is a compile-time reference to the symbol, not a
        // getter/setter access (no accessor runs). Model it as a "Uses" relationship to the property
        // itself (the container), consistent with how fields and methods inside nameof are handled.
        if (accessExpression.IsInsideNameOf(semanticModel))
        {
            AddRelationshipWithFallbackToContainingType(sourceElement, propertySymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
            return;
        }

        if (_config.SplitPropertyAccessors)
        {
            var accessKind = PropertyAccessClassifier.Classify(accessExpression);

            var addedGetter = accessKind is PropertyAccessKind.Read or PropertyAccessKind.ReadWrite &&
                              TryAddAccessorRelationship(sourceElement, propertySymbol.GetMethod, relationshipType, location);
            var addedSetter = accessKind is PropertyAccessKind.Write or PropertyAccessKind.ReadWrite &&
                              TryAddAccessorRelationship(sourceElement, propertySymbol.SetMethod, relationshipType, location);

            if (addedGetter || addedSetter)
            {
                return;
            }

            // No internal accessor element found (external property): fall through to the default below.
        }

        AddRelationshipWithFallbackToContainingType(sourceElement, propertySymbol, relationshipType, [location], RelationshipAttribute.None);
    }

    /// <summary>
    ///     Adds a relationship to the internal code element of a property accessor. Returns false when the
    ///     accessor does not exist (e.g. read-only property) or is external (not in our map).
    /// </summary>
    private bool TryAddAccessorRelationship(CodeElement sourceElement, IMethodSymbol? accessor,
        RelationshipType relationshipType, SourceLocation location)
    {
        if (accessor is null)
        {
            return false;
        }

        var accessorElement = FindInternalCodeElement(accessor);
        if (accessorElement is null)
        {
            return false;
        }

        AddRelationship(sourceElement, relationshipType, accessorElement, [location], RelationshipAttribute.None);
        return true;
    }

    /// <summary>
    ///     Adds a relationship to a symbol, with configurable fallback behavior for external symbols.
    ///     Tries in order: direct symbol → normalized symbol → containing type → external element
    ///     For external symbols, creates relationships to the CONTAINING TYPE only.
    ///     Example: myList.Add(5) -> relationship to List&lt;T&gt; (not to List&lt;T&gt;.Add)
    /// </summary>
    private void AddRelationshipWithFallbackToContainingType(CodeElement sourceElement, ISymbol targetSymbol,
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
    private INamedTypeSymbol? GetTypeSymbolForExternal(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            { ContainingType: not null } => symbol.ContainingType,
            _ => null
        };
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

        // Indexer parameter types. Empty for normal properties.
        foreach (var parameter in propertySymbol.Parameters)
        {
            AddTypeRelationship(propertyElement, parameter.Type, RelationshipType.Uses, propertyLocation);
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
            AddPropertyRelationship(propertyElement, overriddenProperty, RelationshipType.Overrides,
                propertySymbol.GetSymbolLocations());
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

        var accessorElement = FindInternalCodeElement(accessor);
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
            ? FindInternalCodeElement(propertySymbol.GetMethod) ?? propertyElement
            : propertyElement;
        var setElement = _config.SplitPropertyAccessors
            ? FindInternalCodeElement(propertySymbol.SetMethod) ?? propertyElement
            : propertyElement;

        foreach (var syntaxReference in propertySymbol.DeclaringSyntaxReferences)
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
                        // An expression-bodied property/indexer is the getter.
                        AnalyzeMethodBody(getElement, expressionBody.Expression, semanticModel);
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
                                AnalyzeMethodBody(accessorElement, accessor.ExpressionBody.Expression, semanticModel);
                            }
                            else if (accessor.Body != null)
                            {
                                AnalyzeMethodBody(accessorElement, accessor.Body, semanticModel);
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
                        AnalyzeMethodBody(propertyElement, propertyWithInitializer.Initializer.Value, semanticModel, true);
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