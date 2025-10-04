using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Specialized walker for lambda/anonymous method bodies.
///     Only tracks type relationships (object creation, variable declarations) but NOT method calls.
///     This reflects the fact that we know what types are needed to define the lambda,
///     but we don't know when/if the lambda will execute its method calls.
/// </summary>
internal class LambdaBodyWalker : CSharpSyntaxWalker
{
    private readonly ISyntaxNodeHandler _analyzer;
    private readonly SemanticModel _semanticModel;
    private readonly CodeElement _sourceElement;

    public LambdaBodyWalker(ISyntaxNodeHandler analyzer, CodeElement sourceElement, SemanticModel semanticModel)
    {
        _analyzer = analyzer;
        _sourceElement = sourceElement;
        _semanticModel = semanticModel;
    }

    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        // Track local variable declarations - the method needs to know these types
        _analyzer.AnalyzeLocalDeclaration(_sourceElement, node, _semanticModel);
        base.VisitLocalDeclarationStatement(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        // Track object creation - the method needs to know these types

        // Use "Uses" relationship instead of "Creates" for lambdas
        var typeInfo = _semanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            var location = node.GetSyntaxLocation();
            _analyzer.AddTypeRelationshipPublic(_sourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }

        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        // Track object creation - the method needs to know these types

        // Use "Uses" relationship instead of "Creates" for lambdas
        var typeInfo = _semanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            var location = node.GetSyntaxLocation();
            _analyzer.AddTypeRelationshipPublic(_sourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }

        base.VisitObjectCreationExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Do NOT track invocations - we don't know when the lambda executes
        // Skip - don't call base to avoid descending into arguments
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        // Do NOT track assignments (property/field access, event registration)
        // Skip - don't call base
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Do NOT track identifier references
        // Skip - don't call base
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Do NOT track member access
        // Skip - don't call base
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