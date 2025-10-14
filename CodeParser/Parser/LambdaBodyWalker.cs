using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Specialized walker for lambda/anonymous method bodies.
///     Tracks type relationships (object creation, variable declarations) and method/member references
///     using "Uses" relationships (not "Calls" or "Creates").
///     This reflects the fact that we know what types and members are referenced to define the lambda,
///     but we don't know when/if the lambda will execute (hence "Uses" instead of "Calls").
/// </summary>
internal class LambdaBodyWalker : SyntaxWalkerBase
{
    public LambdaBodyWalker(ISyntaxNodeHandler analyzer, CodeElement sourceElement, SemanticModel semanticModel)
        : base(analyzer, sourceElement, semanticModel, false)
    {
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        TrackObjectCreationAsUses(node);
        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        TrackObjectCreationAsUses(node);
        base.VisitObjectCreationExpression(node);
    }

    /// <summary>
    ///     Tracks object creation with "Uses" relationship (not "Creates" for lambdas).
    ///     Handles both implicit (new()) and explicit (new Foo()) object creation.
    /// </summary>
    private void TrackObjectCreationAsUses(BaseObjectCreationExpressionSyntax node)
    {
        var typeInfo = SemanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            var location = node.GetSyntaxLocation();
            Analyzer.AddTypeRelationshipPublic(SourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Track method references with "Uses" relationship (not "Calls")
        // We don't know when the lambda executes, but we know it references these methods
        
        // Code is similar to MethodBodyWalker but does not capture event invocation.
        
        var symbolInfo = SemanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IMethodSymbol calledMethod)
        {
            // Skip local functions - they should not be part of the dependency graph
            if (calledMethod.MethodKind == MethodKind.LocalFunction)
            {
                return;
            }

            var location = node.GetSyntaxLocation();

            // Add "Uses" relationship to the method (with fallback to containing type)
            Analyzer.AddSymbolRelationshipPublic(
                SourceElement,
                calledMethod,
                RelationshipType.Uses,
                [location],
                RelationshipAttribute.None);

            // Handle generic method invocations - track type arguments
            if (calledMethod.IsGenericMethod)
            {
                foreach (var typeArg in calledMethod.TypeArguments)
                {
                    Analyzer.AddTypeRelationshipPublic(SourceElement, typeArg, RelationshipType.Uses, location);
                }
            }
        }

        // Added to capture x => Foo(Bar())
        base.VisitInvocationExpression(node);
    }

    // ReSharper disable once RedundantOverriddenMember
    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        // We need to walk further to capture following expressions:
        // Traversal.Dfs(newParent, n => n.FullName = n.GetFullPath());
        base.VisitAssignmentExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Track member references (properties, fields, events) with "Uses" relationship
        var symbolInfo = SemanticModel.GetSymbolInfo(node);
        var symbol = symbolInfo.Symbol;
        var location = node.GetSyntaxLocation();

        if (symbol is IPropertySymbol propertySymbol)
        {
            Analyzer.AddSymbolRelationshipPublic(
                SourceElement, propertySymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            Analyzer.AddSymbolRelationshipPublic(
                SourceElement, fieldSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            Analyzer.AddSymbolRelationshipPublic(
                SourceElement, eventSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }

        base.VisitMemberAccessExpression(node);
    }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        // Prevent nested lambdas from being analyzed
        // Skip nested lambda
    }

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        // Skip nested lambda
    }

    public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        // Skip nested anonymous method
    }
}