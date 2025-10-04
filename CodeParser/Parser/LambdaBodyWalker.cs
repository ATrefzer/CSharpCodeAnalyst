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
    private readonly CodeElement _sourceElement;
    private readonly SemanticModel _semanticModel;

    public LambdaBodyWalker(ISyntaxNodeHandler analyzer, CodeElement sourceElement, SemanticModel semanticModel)
    {
        _analyzer = analyzer;
        _sourceElement = sourceElement;
        _semanticModel = semanticModel;
    }

    // Track local variable declarations - the method needs to know these types
    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        _analyzer.AnalyzeLocalDeclaration(_sourceElement, node, _semanticModel);
        base.VisitLocalDeclarationStatement(node);
    }

    // Track object creation - the method needs to know these types
    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        // Use "Uses" relationship instead of "Creates" for lambdas
        var typeInfo = _semanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            var location = node.GetSyntaxLocation();
            _analyzer.AddTypeRelationshipPublic(_sourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }
        base.VisitImplicitObjectCreationExpression(node);
    }

    // Track object creation - the method needs to know these types
    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        // Use "Uses" relationship instead of "Creates" for lambdas
        var typeInfo = _semanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            var location = node.GetSyntaxLocation();
            _analyzer.AddTypeRelationshipPublic(_sourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }
        base.VisitObjectCreationExpression(node);
    }

    // Do NOT track invocations - we don't know when the lambda executes
    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Skip - don't call base to avoid descending into arguments
    }

    // Do NOT track assignments (property/field access, event registration)
    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        // Skip - don't call base
    }

    // Do NOT track identifier references
    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Skip - don't call base
    }

    // Do NOT track member access
    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Skip - don't call base
    }

    // Prevent nested lambdas from being analyzed
    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
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