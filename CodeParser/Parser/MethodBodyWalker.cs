using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Syntax walker for analyzing method and property bodies.
///     This approach simplifies the analysis by focusing on specific syntax nodes.
/// </summary>
internal class MethodBodyWalker : SyntaxWalkerBase
{
  
    public MethodBodyWalker(ISyntaxNodeHandler analyzer, CodeElement sourceElement, SemanticModel semanticModel, bool isFieldInitializer)
        : base(analyzer, sourceElement, semanticModel, isFieldInitializer)
    {
    }

    /// <summary>
    ///     Visit standalone identifiers (properties, fields, etc.).
    ///     Uses "Calls" relationship for method bodies.
    ///     Examples: MyProperty (standalone), not obj.MyProperty (that's MemberAccess)
    /// </summary>
    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        Analyzer.AnalyzeIdentifier(SourceElement, node, SemanticModel, RelationshipType.Calls);
        base.VisitIdentifierName(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        Analyzer.AnalyzeInvocation(SourceElement, node, SemanticModel);
        // Note: We still call base to visit arguments, but AnalyzeInvocation won't re-process them
        base.VisitInvocationExpression(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        Analyzer.AnalyzeAssignment(SourceElement, node, SemanticModel);
        base.VisitAssignmentExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        Analyzer.AnalyzeMemberAccess(SourceElement, node, SemanticModel);

        // Explicitly visit only the Expression (left side: obj in obj.Property)
        // The Name (right side: Property) is already handled by AnalyzeMemberAccess
        // This gives clear ownership: MemberAccess owns the .Name, walker handles .Expression independently
        Visit(node.Expression);
    }

    /// <summary>
    ///     new() is ImplicitObjectCreationExpressionSyntax. So ObjectCreationExpressionSyntax does not detect it.
    /// </summary>
    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        Analyzer.AnalyzeObjectCreation(SourceElement, SemanticModel, node, IsFieldInitializer);
        base.VisitImplicitObjectCreationExpression(node);
    }

    /// <summary>
    /// new Foo()
    /// </summary>
    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        Analyzer.AnalyzeObjectCreation(SourceElement, SemanticModel, node, IsFieldInitializer);
        base.VisitObjectCreationExpression(node);
    }

    /// <summary>
    ///     Lambda expressions: Track types and method/member references with "Uses" relationships.
    ///     x => x.Method()
    /// </summary>
    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        // Use a specialized walker that tracks types and method/member references with "Uses" relationships
        var lambdaWalker = new LambdaBodyWalker(Analyzer, SourceElement, SemanticModel);
        lambdaWalker.Visit(node.Body);
    }

    /// <summary>
    ///     Lambda expressions: Track types and method/member references with "Uses" relationships.
    ///     (x, y) => x.Method()
    /// </summary>
    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        // Use a specialized walker that tracks types and method/member references with "Uses" relationships
        var lambdaWalker = new LambdaBodyWalker(Analyzer, SourceElement, SemanticModel);
        lambdaWalker.Visit(node.Body);
    }

    /// <summary>
    ///     Anonymous methods: Track types and method/member references with "Uses" relationships.
    ///     delegate { Method(); }
    /// </summary>
    public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        // Use a specialized walker that tracks types and method/member references with "Uses" relationships
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (node.Block != null)
        {
            var lambdaWalker = new LambdaBodyWalker(Analyzer, SourceElement, SemanticModel);
            lambdaWalker.Visit(node.Block);
        }
    }
    

}