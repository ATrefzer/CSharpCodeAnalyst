using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
/// Handling here does not distinguish between method or lambda bodies.
/// </summary>
internal class SyntaxWalkerBase  : CSharpSyntaxWalker
{
    protected readonly ISyntaxNodeHandler Analyzer;
    protected readonly CodeElement SourceElement;
    protected readonly SemanticModel SemanticModel;
    protected readonly bool IsFieldInitializer;

    protected SyntaxWalkerBase(ISyntaxNodeHandler analyzer, CodeElement sourceElement, SemanticModel semanticModel, bool isFieldInitializer)
    {
        Analyzer = analyzer;
        SourceElement = sourceElement;
        SemanticModel = semanticModel;
        IsFieldInitializer = isFieldInitializer;
    }
    
    /// <summary>
    /// We need this also for lambdas to capture:  x => Foo(SomeMethod)
    /// </summary>
    public override void VisitArgument(ArgumentSyntax node)
    {
        Analyzer.AnalyzeArgument(SourceElement, node, SemanticModel);
        base.VisitArgument(node);
    }

    // Note: VisitIdentifierName is NOT overridden here because concrete walkers need to specify
    // their relationship type (Calls for MethodBodyWalker, Uses for LambdaBodyWalker).
    // Each walker overrides VisitIdentifierName with the appropriate RelationshipType parameter.

    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        Analyzer.AnalyzeLocalDeclaration(SourceElement, node, SemanticModel);
        base.VisitLocalDeclarationStatement(node);
    }

    /// <summary>
    /// typeof(Foo)
    /// </summary>
    public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        
        // Don't get down to the identifier
        //base.VisitTypeOfExpression(node);
    }
    
    /// <summary>
    /// sizeof(Foo)
    /// </summary>
    public override void VisitSizeOfExpression(SizeOfExpressionSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitSizeOfExpression(node);
    }
    
    /// <summary>
    /// default(Foo)
    /// </summary>
    public override void VisitDefaultExpression(DefaultExpressionSyntax node)
    {
        // default(Foo) - Uses relationship to type
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitDefaultExpression(node);
    }
    
    /// <summary>
    /// var x = (Foo)y
    /// </summary>
    public override void VisitCastExpression(CastExpressionSyntax node)
    {
        // (Foo)obj - Uses relationship to cast type
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitCastExpression(node);
    }
    
    /// <summary>
    /// var x = y as Foo
    /// var x = y is Foo
    /// </summary>
    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        // Note: var y = x as BaseClass; would capture the using to BaseClass twice. See AnalyzeLocalDeclaration.
        // Handle: obj is Foo
        if (node.IsKind(SyntaxKind.IsExpression) || node.IsKind(SyntaxKind.AsExpression))
        {
            var typeInfo = SemanticModel.GetTypeInfo(node.Right);
            if (typeInfo.Type != null)
            {
                var location = node.Right.GetSyntaxLocation();
                Analyzer.AddTypeRelationshipPublic(SourceElement, typeInfo.Type, RelationshipType.Uses, location);
            }
        }
        base.VisitBinaryExpression(node);
    }
}