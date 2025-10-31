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

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        // Track event registration/unregistration (handled by AnalyzeAssignment)
        // Property/field access on both sides is handled by the walker's normal traversal,
        // which correctly uses "Uses" relationships for lambdas (see VisitIdentifierName and VisitMemberAccessExpression)
        Analyzer.AnalyzeAssignment(SourceElement, node, SemanticModel);
        base.VisitAssignmentExpression(node);
    }

    /// <summary>
    ///     Visit standalone identifiers (properties, fields, etc.).
    ///     Uses "Uses" relationship for lambda bodies (we don't know when/if lambda executes).
    ///     Examples: x => MyProperty (standalone), not obj.MyProperty (that's MemberAccess)
    /// </summary>
    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        Analyzer.AnalyzeIdentifier(SourceElement, node, SemanticModel, RelationshipType.Uses);
        base.VisitIdentifierName(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Delegate to AnalyzeMemberAccess with "Uses" relationship type for lambdas
        // Same rationale as VisitAssignmentExpression - we don't know when/if the lambda executes
        Analyzer.AnalyzeMemberAccess(SourceElement, node, SemanticModel, RelationshipType.Uses);

        // Now safe to call base traversal since VisitIdentifierName is overridden in this class
        // and will use RelationshipType.Uses (not the default Calls)
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