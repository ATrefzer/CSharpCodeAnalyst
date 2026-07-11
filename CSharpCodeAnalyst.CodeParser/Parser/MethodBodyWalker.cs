using CSharpCodeAnalyst.CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

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
        Analyzer.AnalyzeIdentifier(SourceElement, node, SemanticModel);
        base.VisitIdentifierName(node);
    }

    /// <summary>
    ///     Standalone generic names: the method group "Create&lt;Widget&gt;" is a GenericNameSyntax, not an
    ///     IdentifierNameSyntax. Generic names in type positions (List&lt;Foo&gt; x) resolve to a type
    ///     symbol and are ignored by AnalyzeIdentifier; as invocation target (Create&lt;Widget&gt;()) the
    ///     method-group guard applies. The .Name of a member access is never visited (see
    ///     VisitMemberAccessExpression), so there is no double handling.
    /// </summary>
    public override void VisitGenericName(GenericNameSyntax node)
    {
        Analyzer.AnalyzeIdentifier(SourceElement, node, SemanticModel);
        base.VisitGenericName(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        Analyzer.AnalyzeInvocation(SourceElement, node, SemanticModel);
        // Note: We still call base to visit arguments, but AnalyzeInvocation won't re-process them
        base.VisitInvocationExpression(node);
    }

    /// <summary>
    ///     Constructor chaining: ": base(...)" and ": this(...)".
    /// </summary>
    public override void VisitConstructorInitializer(ConstructorInitializerSyntax node)
    {
        Analyzer.AnalyzeConstructorInitializer(SourceElement, node, SemanticModel);
        // Still visit the argument list so method groups / nested expressions are captured.
        base.VisitConstructorInitializer(node);
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
    ///     Indexer access: store[key]. The receiver and the index arguments are covered by base.
    /// </summary>
    public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        Analyzer.AnalyzeElementAccess(SourceElement, node, SemanticModel);
        base.VisitElementAccessExpression(node);
    }

    /// <summary>
    ///     Conditional indexer access: store?[key]. The "[key]" part is an ElementBindingExpressionSyntax,
    ///     not an ElementAccessExpressionSyntax, so it needs its own visit.
    /// </summary>
    public override void VisitElementBindingExpression(ElementBindingExpressionSyntax node)
    {
        Analyzer.AnalyzeElementAccess(SourceElement, node, SemanticModel);
        base.VisitElementBindingExpression(node);
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
    ///     new Foo()
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

    /// <summary>
    ///     LINQ query syntax. The implicit query-pattern calls (Where/Select/...) run when the query is
    ///     built - real "Calls" of this method. The source of the first from clause is evaluated eagerly
    ///     too, so it keeps method-body semantics. Everything else (clause expressions) is compiled into
    ///     lambdas and gets the lambda "Uses" semantics. base is NOT called - the two visits below cover
    ///     the whole query.
    /// </summary>
    public override void VisitQueryExpression(QueryExpressionSyntax node)
    {
        Analyzer.AnalyzeQueryExpression(SourceElement, node, SemanticModel);

        Visit(node.FromClause.Expression);

        var lambdaWalker = new LambdaBodyWalker(Analyzer, SourceElement, SemanticModel);
        lambdaWalker.Visit(node.Body);
    }
}