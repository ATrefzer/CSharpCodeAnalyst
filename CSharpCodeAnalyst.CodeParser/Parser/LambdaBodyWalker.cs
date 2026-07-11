using CSharpCodeAnalyst.CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

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

    /// <summary>
    ///     Operator/conversion references in a lambda body are "Uses" like every other member reference.
    /// </summary>
    protected override RelationshipType MemberReferenceType => RelationshipType.Uses;

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
    ///     Tracks object creation with "Uses" relationships (not "Creates"/"Calls" for lambdas).
    ///     Handles both implicit (new()) and explicit (new Foo()) object creation. We record both the type
    ///     and - mirroring how method invocations in lambdas are handled - the referenced constructor, so a
    ///     constructor used only inside a lambda does not appear unused.
    /// </summary>
    private void TrackObjectCreationAsUses(BaseObjectCreationExpressionSyntax node)
    {
        var typeInfo = SemanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            var location = node.GetSyntaxLocation();
            Analyzer.AddTypeRelationship(SourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }

        Analyzer.AddConstructorReferenceFromLambda(SourceElement, node, SemanticModel);
    }

    /// <summary>
    ///     Same handler as MethodBodyWalker, but with "Uses" semantics: the invocation is deferred, so
    ///     no call-style attributes and no event-invoke detection (see AnalyzeInvocation).
    /// </summary>
    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        Analyzer.AnalyzeInvocation(SourceElement, node, SemanticModel, RelationshipType.Uses);

        // Visit the arguments: x => Foo(Bar())
        base.VisitInvocationExpression(node);
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

    /// <summary>
    ///     Standalone generic names (method group "Create&lt;Widget&gt;") - see MethodBodyWalker.
    /// </summary>
    public override void VisitGenericName(GenericNameSyntax node)
    {
        Analyzer.AnalyzeIdentifier(SourceElement, node, SemanticModel, RelationshipType.Uses);
        base.VisitGenericName(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Delegate to AnalyzeMemberAccess with "Uses" relationship type for lambdas.
        Analyzer.AnalyzeMemberAccess(SourceElement, node, SemanticModel, RelationshipType.Uses);

        // Visit only the Expression (left side: obj in obj.Member). AnalyzeMemberAccess already owns the
        // .Name, so - like MethodBodyWalker - we must not descend into it via base (that would re-run
        // VisitIdentifierName on the member name and only AddRelationship dedup would hide the double work).
        Visit(node.Expression);
    }

    /// <summary>
    ///     Indexer access in a lambda body: "Uses" like every other member reference in a lambda.
    /// </summary>
    public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        Analyzer.AnalyzeElementAccess(SourceElement, node, SemanticModel, RelationshipType.Uses);
        base.VisitElementAccessExpression(node);
    }

    /// <summary>
    ///     Conditional indexer access in a lambda body: store?[key].
    /// </summary>
    public override void VisitElementBindingExpression(ElementBindingExpressionSyntax node)
    {
        Analyzer.AnalyzeElementAccess(SourceElement, node, SemanticModel, RelationshipType.Uses);
        base.VisitElementBindingExpression(node);
    }

    /// <summary>
    ///     A query expression inside a lambda is deferred as a whole: the implicit query-pattern calls
    ///     and the clause expressions are all "Uses". The default traversal already applies the right
    ///     semantics to the clause expressions, so base is called normally.
    /// </summary>
    public override void VisitQueryExpression(QueryExpressionSyntax node)
    {
        Analyzer.AnalyzeQueryExpression(SourceElement, node, SemanticModel, RelationshipType.Uses);
        base.VisitQueryExpression(node);
    }

    // Nested lambdas are deliberately NOT skipped: the body of an inner lambda is "deferred twice",
    // which is still deferred - the Uses semantics of this walker apply unchanged, so the default
    // traversal simply descends into it. (They used to be skipped, silently losing every dependency
    // inside the inner lambda.)
}