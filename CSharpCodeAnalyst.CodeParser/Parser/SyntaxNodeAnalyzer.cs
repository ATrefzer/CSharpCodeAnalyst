using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Turns the syntax nodes reported by the body walkers (<see cref="MethodBodyWalker" /> /
///     <see cref="LambdaBodyWalker" />) into relationships: invocations, member/element accesses,
///     object creations, operators, conversions, queries, deconstructions, ... The relationship
///     recording itself (symbol resolution, normalization, external fallback, locking) is delegated
///     to the <see cref="RelationshipBuilder" />.
/// </summary>
internal class SyntaxNodeAnalyzer : ISyntaxNodeHandler
{
    private readonly RelationshipBuilder _builder;
    private readonly ParserConfig _config;

    internal SyntaxNodeAnalyzer(RelationshipBuilder builder, ParserConfig config)
    {
        _builder = builder;
        _config = config;
    }

    /// <summary>
    ///     For method and property bodies, field initializers, attribute arguments, enum member
    ///     initializers and primary-constructor base arguments. Entry point for every body walk.
    /// </summary>
    public void AnalyzeMethodBody(CodeElement sourceElement, SyntaxNode node, SemanticModel semanticModel, bool isFieldInitializer = false)
    {
        var walker = new MethodBodyWalker(this, sourceElement, semanticModel, isFieldInitializer);
        walker.Visit(node);
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
            _builder.AddCallsRelationship(sourceElement, calledMethod, location, attributes);

            // Handle generic method invocations
            if (calledMethod.IsGenericMethod)
            {
                foreach (var typeArg in calledMethod.TypeArguments)
                {
                    _builder.AddTypeRelationship(sourceElement, typeArg, RelationshipType.Uses, location);
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
            if (normalizedConstructor.IsExplicitConstructor() && _builder.FindInternalCodeElement(normalizedConstructor) is not null)
            {
                var attribute = initializerSyntax.IsKind(SyntaxKind.BaseConstructorInitializer)
                    ? RelationshipAttribute.IsBaseCall
                    : RelationshipAttribute.IsThisCall;
                _builder.AddCallsRelationship(sourceElement, normalizedConstructor, initializerSyntax.GetSyntaxLocation(), attribute);
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
        _builder.AddTypeRelationship(sourceElement, typeSymbol, relationshipType, location);
    }

    /// <summary>
    ///     Public wrapper for AddRelationshipWithFallbackToContainingType to allow access from LambdaBodyWalker.
    ///     Adds a relationship to a symbol (method, property, field, event), with fallback to containing type for external
    ///     symbols.
    /// </summary>
    public void AddSymbolRelationshipPublic(CodeElement sourceElement, ISymbol targetSymbol,
        RelationshipType relationshipType, List<SourceLocation>? locations, RelationshipAttribute attributes)
    {
        _builder.AddRelationshipWithFallbackToContainingType(sourceElement, targetSymbol, relationshipType, locations, attributes);
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
            if (normalizedConstructor.IsExplicitConstructor() && _builder.FindInternalCodeElement(normalizedConstructor) is not null)
            {
                var location = objectCreationSyntax.GetSyntaxLocation();
                _builder.AddRelationshipWithFallbackToContainingType(sourceElement, normalizedConstructor,
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
            _builder.AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeIdentifier" />
    /// </summary>
    public void AnalyzeIdentifier(CodeElement sourceElement, SimpleNameSyntax identifierSyntax,
        SemanticModel semanticModel, RelationshipType propertyAccessType = RelationshipType.Calls)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(identifierSyntax);
        var symbol = symbolInfo.Symbol ?? SingleMethodGroupCandidate(symbolInfo);

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
            _builder.AddRelationshipWithFallbackToContainingType(sourceElement, fieldSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            var location = identifierSyntax.GetSyntaxLocation();
            AddEventUsageRelationship(sourceElement, eventSymbol, location);
        }
        else if (symbol is IMethodSymbol methodSymbol && IsMethodGroupReference(identifierSyntax, methodSymbol))
        {
            // Foo passed/assigned/returned as a delegate (method group), not invoked.
            AddMethodGroupRelationship(sourceElement, methodSymbol, identifierSyntax.GetSyntaxLocation());
        }
        else if (identifierSyntax is GenericNameSyntax && symbol is INamedTypeSymbol { IsGenericType: true } constructedType)
        {
            // A constructed generic type named in expression position (Registry<Token>.Instance):
            // the member edge is normalized to Registry<T>, so the type arguments would be lost.
            // In type positions the same edges are produced by the declaration handlers and merge.
            var location = identifierSyntax.GetSyntaxLocation();
            foreach (var typeArg in constructedType.TypeArguments)
            {
                _builder.AddTypeRelationship(sourceElement, typeArg, RelationshipType.Uses, location);
            }
        }
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeMemberAccess" />
    /// </summary>
    public void AnalyzeMemberAccess(CodeElement sourceElement, MemberAccessExpressionSyntax memberAccessSyntax,
        SemanticModel semanticModel, RelationshipType propertyAccessType = RelationshipType.Calls)
    {
        // Analyze the member being accessed (the right side of the dot)
        var symbolInfo = semanticModel.GetSymbolInfo(memberAccessSyntax);
        var symbol = symbolInfo.Symbol ?? SingleMethodGroupCandidate(symbolInfo);

        if (symbol is IPropertySymbol propertySymbol)
        {
            var location = memberAccessSyntax.GetSyntaxLocation();
            AddPropertyAccessRelationship(sourceElement, propertySymbol, memberAccessSyntax, propertyAccessType, location, semanticModel);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            var location = memberAccessSyntax.GetSyntaxLocation();
            _builder.AddRelationshipWithFallbackToContainingType(sourceElement, fieldSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            // This handles cases where the event is accessed but not necessarily invoked
            AddEventUsageRelationship(sourceElement, eventSymbol, memberAccessSyntax.GetSyntaxLocation());
        }
        else if (symbol is IMethodSymbol methodSymbol && IsMethodGroupReference(memberAccessSyntax, methodSymbol))
        {
            // obj.Foo passed/assigned/returned as a delegate (method group), not invoked.
            AddMethodGroupRelationship(sourceElement, methodSymbol, memberAccessSyntax.GetSyntaxLocation());
        }

        // Note: We don't recursively handle the Expression here.
        // The walker's Visit(node.Expression) call handles nested member access automatically.
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeOperatorUsage" />
    /// </summary>
    public void AnalyzeOperatorUsage(CodeElement sourceElement, ExpressionSyntax expression,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls)
    {
        // Built-in operators (int +, string +, delegate +=, ...) bind to MethodKind.BuiltinOperator
        // and are skipped; only user-defined operators and conversions are real code elements.
        if (semanticModel.GetSymbolInfo(expression).Symbol is IMethodSymbol
            {
                MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion
            } operatorMethod)
        {
            AddOperatorRelationship(sourceElement, operatorMethod, expression, relationshipType);
        }
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeImplicitConversion" />
    /// </summary>
    public void AnalyzeImplicitConversion(CodeElement sourceElement, ExpressionSyntax expression,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls)
    {
        var conversion = semanticModel.GetConversion(expression);
        if (conversion is { IsUserDefined: true, MethodSymbol: not null })
        {
            AddOperatorRelationship(sourceElement, conversion.MethodSymbol, expression, relationshipType);
        }
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

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeQueryExpression" />
    /// </summary>
    public void AnalyzeQueryExpression(CodeElement sourceElement, QueryExpressionSyntax querySyntax,
        SemanticModel semanticModel, RelationshipType operatorCallType = RelationshipType.Calls)
    {
        // A typed from clause ("from Foo x in xs") inserts a Cast<Foo>() call.
        var fromClauseInfo = semanticModel.GetQueryClauseInfo(querySyntax.FromClause);
        AddQueryOperatorRelationship(sourceElement, fromClauseInfo.CastInfo, querySyntax.FromClause, operatorCallType);

        AnalyzeQueryBody(sourceElement, querySyntax.Body, semanticModel, operatorCallType);
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeDeconstruction" />
    /// </summary>
    public void AnalyzeDeconstruction(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls)
    {
        // Only a simple assignment whose left side is a tuple ("(a, b) = ...") or a declaration
        // ("var (x, y) = ...") can deconstruct.
        if (!assignmentExpression.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
            assignmentExpression.Left is not (TupleExpressionSyntax or DeclarationExpressionSyntax))
        {
            return;
        }

        var deconstruction = semanticModel.GetDeconstructionInfo(assignmentExpression);
        AddDeconstructionRelationships(sourceElement, deconstruction, assignmentExpression, relationshipType);
    }

    /// <summary>
    ///     <inheritdoc cref="ISyntaxNodeHandler.AnalyzeForEachStatement" />
    /// </summary>
    public void AnalyzeForEachStatement(CodeElement sourceElement, CommonForEachStatementSyntax forEachSyntax,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls)
    {
        var info = semanticModel.GetForEachStatementInfo(forEachSyntax);
        if (info.GetEnumeratorMethod is not null)
        {
            _builder.AddSynthesizedCallRelationship(sourceElement, info.GetEnumeratorMethod, forEachSyntax, relationshipType);
        }

        // foreach (var (x, y) in pairs) deconstructs each element.
        if (forEachSyntax is ForEachVariableStatementSyntax forEachVariable)
        {
            var deconstruction = semanticModel.GetDeconstructionInfo(forEachVariable);
            AddDeconstructionRelationships(sourceElement, deconstruction, forEachSyntax, relationshipType);
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
            _builder.AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Uses, location);
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
                _builder.AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Uses, location);

                // Containing class "creates" the object
                if (sourceElement.Parent != null)
                {
                    _builder.AddTypeRelationship(sourceElement.Parent, typeInfo.Type, RelationshipType.Creates, location);
                }
            }
            else
            {
                // Method "creates" the object
                _builder.AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Creates, location);
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
                if (normalizedConstructor.IsExplicitConstructor() && _builder.FindInternalCodeElement(normalizedConstructor) is not null)
                {
                    var location = objectCreationSyntax.GetSyntaxLocation();
                    _builder.AddCallsRelationship(sourceElement, normalizedConstructor, location, RelationshipAttribute.None);
                }
            }
        }

        // Note: Arguments (including method groups passed as arguments) are handled by the walker's
        // normal traversal into the argument expressions (AnalyzeIdentifier / AnalyzeMemberAccess).
    }

    /// <summary>
    ///     A method group converted to System.Delegate ("Register(Create&lt;Widget&gt;)" with a Delegate
    ///     parameter) binds to no symbol: Roslyn reports OverloadResolutionFailure and puts the group's
    ///     members into the candidates - even though the code compiles. With exactly one method candidate
    ///     the reference is unambiguous, so we use it. (Func/Action-typed positions bind normally.)
    /// </summary>
    private static ISymbol? SingleMethodGroupCandidate(SymbolInfo symbolInfo)
    {
        if (symbolInfo is { CandidateReason: CandidateReason.OverloadResolutionFailure, CandidateSymbols: [IMethodSymbol candidate] })
        {
            return candidate;
        }

        return null;
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
    ///     Records the "Uses" edge (IsMethodGroup) for a method group reference. For a constructed generic
    ///     method group (Create&lt;Widget&gt;) the type arguments are real dependencies too, mirroring the
    ///     generic handling in AnalyzeInvocation.
    /// </summary>
    private void AddMethodGroupRelationship(CodeElement sourceElement, IMethodSymbol methodSymbol, SourceLocation location)
    {
        _builder.AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Uses, [location], RelationshipAttribute.IsMethodGroup);

        if (methodSymbol.IsGenericMethod)
        {
            foreach (var typeArg in methodSymbol.TypeArguments)
            {
                _builder.AddTypeRelationship(sourceElement, typeArg, RelationshipType.Uses, location);
            }
        }
    }

    private void AddOperatorRelationship(CodeElement sourceElement, IMethodSymbol operatorMethod,
        ExpressionSyntax expression, RelationshipType relationshipType)
    {
        // Operators of generic types (Money<T>) come back constructed; normalize like any other member.
        // External operators (e.g. decimal arithmetic, DateTime -) fall back to their containing type.
        var normalizedOperator = operatorMethod.NormalizeToOriginalDefinition();
        _builder.AddRelationshipWithFallbackToContainingType(sourceElement, normalizedOperator, relationshipType,
            [expression.GetSyntaxLocation()], RelationshipAttribute.None);
    }

    private void AnalyzeQueryBody(CodeElement sourceElement, QueryBodySyntax body, SemanticModel semanticModel,
        RelationshipType operatorCallType)
    {
        foreach (var clause in body.Clauses)
        {
            if (clause is OrderByClauseSyntax orderByClause)
            {
                // The operator (OrderBy/OrderByDescending/ThenBy/ThenByDescending) hangs on each
                // ordering, not on the clause.
                foreach (var ordering in orderByClause.Orderings)
                {
                    AddQueryOperatorRelationship(sourceElement, semanticModel.GetSymbolInfo(ordering), ordering, operatorCallType);
                }
            }
            else
            {
                // where -> Where, let -> Select, join -> (Group)Join, from -> SelectMany, ...
                var clauseInfo = semanticModel.GetQueryClauseInfo(clause);
                AddQueryOperatorRelationship(sourceElement, clauseInfo.OperationInfo, clause, operatorCallType);
                AddQueryOperatorRelationship(sourceElement, clauseInfo.CastInfo, clause, operatorCallType);
            }
        }

        // select -> Select (absent for a degenerate "select x"), group -> GroupBy.
        AddQueryOperatorRelationship(sourceElement, semanticModel.GetSymbolInfo(body.SelectOrGroup), body.SelectOrGroup, operatorCallType);

        // "... into g ..." continues with a fresh clause list.
        if (body.Continuation is not null)
        {
            AnalyzeQueryBody(sourceElement, body.Continuation.Body, semanticModel, operatorCallType);
        }
    }

    private void AddQueryOperatorRelationship(CodeElement sourceElement, SymbolInfo symbolInfo, SyntaxNode clause,
        RelationshipType operatorCallType)
    {
        if (symbolInfo.Symbol is IMethodSymbol operatorMethod)
        {
            _builder.AddSynthesizedCallRelationship(sourceElement, operatorMethod, clause, operatorCallType);
        }
    }

    private void AddDeconstructionRelationships(CodeElement sourceElement, DeconstructionInfo deconstruction,
        SyntaxNode node, RelationshipType relationshipType)
    {
        // Pure tuple deconstructions have no method; user-defined Deconstruct does.
        if (deconstruction.Method is not null)
        {
            _builder.AddSynthesizedCallRelationship(sourceElement, deconstruction.Method, node, relationshipType);
        }

        // Nested deconstructions: var (a, (b, c)) = ...
        foreach (var nested in deconstruction.Nested)
        {
            AddDeconstructionRelationships(sourceElement, nested, node, relationshipType);
        }
    }

    private void AddEventInvocationRelationship(CodeElement sourceElement, IEventSymbol eventSymbol,
        SourceLocation location)
    {
        _builder.AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, RelationshipType.Invokes, [location], RelationshipAttribute.None);
    }

    private void AddEventUsageRelationship(CodeElement sourceElement, IEventSymbol eventSymbol, SourceLocation location, RelationshipAttribute attribute = RelationshipAttribute.None)
    {
        _builder.AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, RelationshipType.Uses, [location], attribute);
    }

    private void AddEventHandlerRelationship(IMethodSymbol handlerMethod, IEventSymbol eventSymbol,
        SourceLocation location, RelationshipAttribute attribute)
    {
        var handlerElement = _builder.FindInternalCodeElement(handlerMethod);
        var eventElement = _builder.FindInternalCodeElement(eventSymbol);

        if (handlerElement != null && eventElement != null)
        {
            _builder.AddRelationship(handlerElement, RelationshipType.Handles, eventElement, [location], attribute);
        }
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
            _builder.AddRelationshipWithFallbackToContainingType(sourceElement, propertySymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
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

        _builder.AddRelationshipWithFallbackToContainingType(sourceElement, propertySymbol, relationshipType, [location], RelationshipAttribute.None);
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

        var accessorElement = _builder.FindInternalCodeElement(accessor);
        if (accessorElement is null)
        {
            return false;
        }

        _builder.AddRelationship(sourceElement, relationshipType, accessorElement, [location], RelationshipAttribute.None);
        return true;
    }

    private static RelationshipAttribute DetermineCallAttributes(InvocationExpressionSyntax invocation,
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
