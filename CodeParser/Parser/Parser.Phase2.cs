﻿using System.Diagnostics;
using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

public partial class Parser
{
    /// <summary>
    ///     Entry for relationship analysis
    /// </summary>
    private void AnalyzeRelationships(Solution solution)
    {
        var numberOfCodeElements = _codeGraph.Nodes.Count;

        var loop = 0;
        foreach (var element in _codeGraph.Nodes.Values)
        {
            if (!_elementIdToSymbolMap.TryGetValue(element.Id, out var symbol))
            {
                // INamespaceSymbol
                continue;
            }

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

            SendParserPhase2Progress(loop++, numberOfCodeElements);
        }

        // Analyze global statements for each assembly
        AnalyzeGlobalStatementsForAssembly(solution);
    }

    private void SendParserPhase2Progress(int loop, int numberOfCodeElements)
    {
        if (loop % 10 == 0)
        {
            var percent = Math.Floor(loop / (double)numberOfCodeElements * 100);
            var msg = $"Phase 2/2: Analyzing relationships. Finished {percent}%.";
            var args = new ParserProgressArg(msg);

            ParserProgress?.Invoke(this, args);
        }
    }

    private void AnalyzeGlobalStatementsForAssembly(Solution solution)
    {
        foreach (var statement in _globalStatementsByAssembly)
        {
            var assemblySymbol = statement.Key;
            var globalStatements = statement.Value;
            if (globalStatements.Count == 0)
            {
                continue;
            }

            // Find the existing assembly element
            var symbolKey = assemblySymbol.Key();
            var assemblyElement = _symbolKeyToElementMap[symbolKey];

            // Create a dummy class for this assembly's global statements
            var dummyClassId = Guid.NewGuid().ToString();
            var dummyClassName = "GlobalStatements";
            var dummyClassFullName = assemblySymbol.BuildSymbolName() + "." + dummyClassName;
            var dummyClass = new CodeElement(dummyClassId, CodeElementType.Class, dummyClassName, dummyClassFullName,
                assemblyElement);
            _codeGraph.Nodes[dummyClassId] = dummyClass;
            assemblyElement.Children.Add(dummyClass);

            // Create a dummy method to contain global statements
            var dummyMethodId = Guid.NewGuid().ToString();
            var dummyMethodName = "Execute";
            var dummyMethodFullName = $"{dummyClassName}.{dummyMethodName}";
            var dummyMethod = new CodeElement(dummyMethodId, CodeElementType.Method, dummyMethodName,
                dummyMethodFullName, dummyClass);
            _codeGraph.Nodes[dummyMethodId] = dummyMethod;
            dummyClass.Children.Add(dummyMethod);

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
                var location = attributeData.ApplicationSyntaxReference != null
                    ? GetLocation(attributeData.ApplicationSyntaxReference.GetSyntax())
                    : null;

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

        // Analyze return type
        AddTypeRelationship(delegateElement, methodSymbol.ReturnType, RelationshipType.Uses);

        // Analyze parameter types
        foreach (var parameter in methodSymbol.Parameters)
        {
            AddTypeRelationship(delegateElement, parameter.Type, RelationshipType.Uses);
        }
    }

    private void AnalyzeEventRelationships(Solution solution, CodeElement eventElement, IEventSymbol eventSymbol)
    {
        // Analyze event type (usually a delegate type)
        AddTypeRelationship(eventElement, eventSymbol.Type, RelationshipType.Uses);

        // Check if this event implements an interface event
        var implementedInterfaceEvent = GetImplementedInterfaceEvent(eventSymbol);
        if (implementedInterfaceEvent != null)
        {
            var locations = GetLocations(eventSymbol);
            AddEventRelationship(eventElement, implementedInterfaceEvent, RelationshipType.Implements, locations);
        }

        // If the event has add/remove accessors, analyze them
        if (eventSymbol.AddMethod != null)
        {
            AnalyzeMethodRelationships(solution, eventElement, eventSymbol.AddMethod);
        }

        if (eventSymbol.RemoveMethod != null)
        {
            AnalyzeMethodRelationships(solution, eventElement, eventSymbol.RemoveMethod);
        }
    }

    private IEventSymbol? GetImplementedInterfaceEvent(IEventSymbol eventSymbol)
    {
        var containingType = eventSymbol.ContainingType;
        foreach (var @interface in containingType.AllInterfaces)
        {
            var interfaceMembers = @interface.GetMembers().OfType<IEventSymbol>();
            foreach (var interfaceEvent in interfaceMembers)
            {
                var implementingEvent = containingType.FindImplementationForInterfaceMember(interfaceEvent);
                if (implementingEvent != null && SymbolEqualityComparer.Default.Equals(implementingEvent, eventSymbol))
                {
                    return interfaceEvent;
                }
            }
        }

        return null;
    }

    private void AddEventRelationship(CodeElement sourceElement, IEventSymbol eventSymbol,
        RelationshipType relationshipType, List<SourceLocation> locations)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, relationshipType, locations);
    }


    /// <summary>
    ///     Use solution, not the compilation. The syntax tree may not be found.
    /// </summary>
    private void AnalyzeMethodRelationships(Solution solution, CodeElement methodElement, IMethodSymbol methodSymbol)
    {
        // Analyze parameter types
        foreach (var parameter in methodSymbol.Parameters)
        {
            AddTypeRelationship(methodElement, parameter.Type, RelationshipType.Uses);
        }

        // Analyze return type
        if (!methodSymbol.ReturnsVoid)
        {
            AddTypeRelationship(methodElement, methodSymbol.ReturnType, RelationshipType.Uses);
        }

        //if (methodSymbol.IsExtensionMethod)
        //{
        //    // The first parameter of an extension method is the extended type
        //    var extendedType = methodSymbol.Parameters[0].Type;
        //    AddTypeRelationship(methodElement, extendedType, RelationshipType.Uses);
        //}

        // If this method is an interface method or an abstract method, find its implementations
        if (methodSymbol.IsAbstract || methodSymbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            FindImplementations(methodElement, methodSymbol);
        }

        // Check for method override
        if (methodSymbol.IsOverride)
        {
            var overriddenMethod = methodSymbol.OverriddenMethod;
            if (overriddenMethod != null)
            {
                var locations = GetLocations(methodSymbol);
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


    private void FindImplementations(CodeElement methodElement, IMethodSymbol methodSymbol)
    {
        var implementingTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // If it's an interface method, find all types implementing the interface
        if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            implementingTypes.UnionWith(FindTypesImplementingInterface(methodSymbol.ContainingType));
        }
        // If it's an abstract method, find all types deriving from the containing type
        else if (methodSymbol.IsAbstract)
        {
            implementingTypes.UnionWith(FindTypesDerivedFrom(methodSymbol.ContainingType));
        }

        foreach (var implementingType in implementingTypes)
        {
            var implementingMethod = implementingType.FindImplementationForInterfaceMember(methodSymbol);
            if (implementingMethod != null)
            {
                var implementingElement = _symbolKeyToElementMap.GetValueOrDefault(implementingMethod.Key());
                if (implementingElement != null)
                {
                    // Note: Implementations for external methods are not in our map
                    var locations = GetLocations(implementingMethod);
                    AddRelationship(implementingElement, RelationshipType.Implements, methodElement, locations);
                }
            }
        }
    }

    private IEnumerable<INamedTypeSymbol> FindTypesImplementingInterface(INamedTypeSymbol interfaceSymbol)
    {
        return _allNamedTypesInSolution
            .Where(type => type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol)));
    }

    private IEnumerable<INamedTypeSymbol> FindTypesDerivedFrom(INamedTypeSymbol baseType)
    {
        return _allNamedTypesInSolution
            .Where(type => IsTypeDerivedFrom(type, baseType));
    }

    private bool IsTypeDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var currentType = type.BaseType;
        while (currentType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentType, baseType))
            {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
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
        AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Overrides, locations);
    }

    private void AnalyzeFieldRelationships(CodeElement fieldElement, IFieldSymbol fieldSymbol)
    {
        AddTypeRelationship(fieldElement, fieldSymbol.Type, RelationshipType.Uses);
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
                    var typeInfo = semanticModel.GetTypeInfo(objectCreationSyntax);
                    if (typeInfo.Type != null)
                    {
                        var location = GetLocation(objectCreationSyntax);
                        AddTypeRelationship(sourceElement, typeInfo.Type, RelationshipType.Creates, location);
                    }

                    break;

                case InvocationExpressionSyntax invocationSyntax:
                    AnalyzeInvocation(sourceElement, invocationSyntax, semanticModel);
                    break;

                case AssignmentExpressionSyntax assignmentExpression:
                    // Property assignments, event registration
                    AnalyzeAssignment(sourceElement, assignmentExpression, semanticModel);
                    break;

                case IdentifierNameSyntax identifierSyntax:
                    AnalyzeIdentifier(sourceElement, identifierSyntax, semanticModel);
                    break;

                case MemberAccessExpressionSyntax memberAccessSyntax:
                    AnalyzeMemberAccess(sourceElement, memberAccessSyntax, semanticModel);
                    break;
            }
        }
    }

    private void AnalyzeInvocation(CodeElement sourceElement, InvocationExpressionSyntax invocationSyntax,
        SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocationSyntax);
        if (symbolInfo.Symbol is IMethodSymbol calledMethod)
        {
            var location = GetLocation(invocationSyntax);
            AddCallsRelationship(sourceElement, calledMethod, location);

            // Handle generic method invocations
            if (calledMethod.IsGenericMethod)
            {
                foreach (var typeArg in calledMethod.TypeArguments)
                {
                    AddTypeRelationship(sourceElement, typeArg, RelationshipType.Uses, location);
                }
            }

            // Check if this is an event invocation using Invoke method
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
                    while (currentNode != null && !(currentNode is ConditionalAccessExpressionSyntax))
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

        // Handle direct event invocations (if any)
        var invokedSymbol = semanticModel.GetSymbolInfo(invocationSyntax.Expression).Symbol;
        //if (invokedSymbol is IMethodSymbol { AssociatedSymbol: IEventSymbol symbol })
        if (invokedSymbol is IEventSymbol symbol)
        {
            AddEventInvocationRelationship(sourceElement, symbol, GetLocation(invocationSyntax));
        }
    }

    private void AddEventInvocationRelationship(CodeElement sourceElement, IEventSymbol eventSymbol,
        SourceLocation location)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, RelationshipType.Invokes, [location]);
    }

    private void AddEventUsageRelationship(CodeElement sourceElement, IEventSymbol eventSymbol, SourceLocation location)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, eventSymbol, RelationshipType.Uses, [location]);
    }

    private void AnalyzeAssignment(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
        SemanticModel semanticModel)
    {
        // Analyze the left side of the assignment (target)
        AnalyzeExpressionForPropertyAccess(sourceElement, assignmentExpression.Left, semanticModel);

        // Analyze the right side of the assignment (value)
        AnalyzeExpressionForPropertyAccess(sourceElement, assignmentExpression.Right, semanticModel);

        // Handle event registration and un-registration
        if (assignmentExpression.IsKind(SyntaxKind.AddAssignmentExpression) ||
            assignmentExpression.IsKind(SyntaxKind.SubtractAssignmentExpression))
        {
            var leftSymbol = semanticModel.GetSymbolInfo(assignmentExpression.Left).Symbol;
            var rightSymbol = semanticModel.GetSymbolInfo(assignmentExpression.Right).Symbol;

            if (leftSymbol is IEventSymbol eventSymbol)
            {
                AddEventUsageRelationship(sourceElement, eventSymbol, GetLocation(assignmentExpression));

                // If the right side is a method, add a Handles relationship
                if (rightSymbol is IMethodSymbol methodSymbol)
                {
                    AddEventHandlerRelationship(methodSymbol, eventSymbol, GetLocation(assignmentExpression));
                }
            }
        }
    }

    private void AddEventHandlerRelationship(IMethodSymbol handlerMethod, IEventSymbol eventSymbol,
        SourceLocation location)
    {
        var handlerElement = FindCodeElement(handlerMethod);
        var eventElement = FindCodeElement(eventSymbol);

        if (handlerElement != null && eventElement != null)
        {
            AddRelationship(handlerElement, RelationshipType.Handles, eventElement, [location]);
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

    private void AddCallsRelationship(CodeElement sourceElement, IMethodSymbol methodSymbol, SourceLocation location)
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
        AddRelationshipWithFallbackToContainingType(sourceElement, methodSymbol, RelationshipType.Calls, [location]);
    }


    /// <summary>
    ///     Handle also List_T. Where List is not a code element of our project
    /// </summary>
    private void AnalyzeInheritanceRelationships(CodeElement element, INamedTypeSymbol typeSymbol)
    {
        // Analyze base class
        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            AddTypeRelationship(element, typeSymbol.BaseType, RelationshipType.Inherits);
        }

        // Analyze implemented interfaces
        foreach (var @interface in typeSymbol.Interfaces)
        {
            AddTypeRelationship(element, @interface, RelationshipType.Implements);
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
            case IFunctionPointerTypeSymbol functionPointerType:

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
                if (_symbolKeyToElementMap.TryGetValue(symbolKey, out var targetElement))
                {
                    AddRelationship(sourceElement, relationshipType, targetElement, location != null ? [location] : []);
                }

                break;
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
            AddRelationship(sourceElement, relationshipType, targetElement, location != null ? [location] : []);
        }
        else
        {
            // The type is external or a constructed generic type
            // Note the constructed type is not in our CodeElement map!
            // It is not found in phase1 the way we parse it but the original definition is.
            var originalDefinition = namedTypeSymbol.OriginalDefinition;
            var originalSymbolKey = originalDefinition.Key();

            if (_symbolKeyToElementMap.TryGetValue(originalSymbolKey, out var originalTargetElement))
            {
                // We found the original definition, add relationship to it
                AddRelationship(sourceElement, relationshipType, originalTargetElement,
                    location != null ? [location] : []);
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

        if (symbol is IPropertySymbol propertySymbol)
        {
            var location = GetLocation(identifierSyntax);
            AddPropertyCallRelationship(sourceElement, propertySymbol, [location]);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            AddRelationshipWithFallbackToContainingType(sourceElement, fieldSymbol, RelationshipType.Uses);
        }
    }


    private void AnalyzeMemberAccess(CodeElement sourceElement, MemberAccessExpressionSyntax memberAccessSyntax,
        SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(memberAccessSyntax);
        var symbol = symbolInfo.Symbol;

        if (symbol is IPropertySymbol propertySymbol)
        {
            var location = GetLocation(memberAccessSyntax);
            AddPropertyCallRelationship(sourceElement, propertySymbol, [location]);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            AddRelationshipWithFallbackToContainingType(sourceElement, fieldSymbol, RelationshipType.Uses);
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            // This handles cases where the event is accessed but not necessarily invoked
            AddEventUsageRelationship(sourceElement, eventSymbol, GetLocation(memberAccessSyntax));
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
        List<SourceLocation> locations)
    {
        AddRelationshipWithFallbackToContainingType(sourceElement, propertySymbol, RelationshipType.Calls, locations);
    }

    private void AddRelationshipWithFallbackToContainingType(CodeElement sourceElement, ISymbol symbol,
        RelationshipType relationshipType, List<SourceLocation>? locations = null)
    {
        // If we don't have the property itself in our map, add a relationship to its containing type
        if (locations == null)
        {
            locations = [];
        }

        var targetElement = FindCodeElement(symbol);
        if (targetElement != null)
        {
            AddRelationship(sourceElement, relationshipType, targetElement, locations);
            return;
        }

        var containingTypeElement = FindCodeElement(symbol.ContainingType);
        if (containingTypeElement != null)
        {
            AddRelationship(sourceElement, relationshipType, containingTypeElement, locations);
        }
    }

    private CodeElement? FindCodeElement(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        _symbolKeyToElementMap.TryGetValue(symbol.Key(), out var element);
        return element;
    }
}