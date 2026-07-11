using CSharpCodeAnalyst.CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     <code>
///     Handling here does not distinguish between method or lambda bodies.
///     | Visit                    | Method                                         | Lambda                                  |
///     |--------------------------|------------------------------------------------|-----------------------------------------|
///     | `IdentifierName`         | `AnalyzeIdentifier` (Calls)                    | `AnalyzeIdentifier` (**Uses**)          |
///     | `Invocation`             | `AnalyzeInvocation` (Calls **+ Event-Invoke**) | `AnalyzeInvocation` (Uses, **no** Event-Invoke) |
///     | `ObjectCreation`         | `AnalyzeObjectCreation` (** Creates**)         | `TrackObjectCreationAsUses` (** Uses**) |
///     | nested Lambdas           | spawns `LambdaBodyWalker`                      | walked with the same Uses semantics     |
///     | `ConstructorInitializer` | yes                                            | no (Lambdas have none)                  |
/// </code>
/// </summary>
internal class SyntaxWalkerBase : CSharpSyntaxWalker
{
    protected readonly ISyntaxNodeHandler Analyzer;
    protected readonly bool IsFieldInitializer;
    protected readonly SemanticModel SemanticModel;
    protected readonly CodeElement SourceElement;

    protected SyntaxWalkerBase(ISyntaxNodeHandler analyzer, CodeElement sourceElement, SemanticModel semanticModel, bool isFieldInitializer)
    {
        Analyzer = analyzer;
        SourceElement = sourceElement;
        SemanticModel = semanticModel;
        IsFieldInitializer = isFieldInitializer;
    }

    // Note: VisitIdentifierName is NOT overridden here because concrete walkers need to specify
    // their relationship type (Calls for MethodBodyWalker, Uses for LambdaBodyWalker).
    // Each walker overrides VisitIdentifierName with the appropriate RelationshipType parameter.

    /// <summary>
    ///     Relationship type for member references (operators, conversions) found by the shared visits
    ///     in this base class. Method bodies record real calls; the lambda walker overrides with "Uses"
    ///     because lambda execution is deferred.
    /// </summary>
    protected virtual RelationshipType MemberReferenceType => RelationshipType.Calls;

    /// <summary>
    ///     Event registration/unregistration (event += / -= handler). Identical for method and lambda
    ///     bodies: the registration itself is the same edge
    ///     Property/field access on either side is covered by the normal identifier/member-access traversal
    ///     ("Uses" relationships for lambdas (see VisitIdentifierName and VisitMemberAccessExpression))
    /// </summary>
    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        Analyzer.AnalyzeEventRegistrationAssignment(SourceElement, node, SemanticModel);

        // Compound assignments (a += b) bind to the user-defined binary operator; the right side of a
        // simple assignment may carry an implicit user-defined conversion (celsius = 21.5).
        Analyzer.AnalyzeOperatorUsage(SourceElement, node, SemanticModel, MemberReferenceType);
        Analyzer.AnalyzeImplicitConversion(SourceElement, node.Right, SemanticModel, MemberReferenceType);

        // "var (x, y) = point" calls the user-defined Deconstruct method.
        Analyzer.AnalyzeDeconstruction(SourceElement, node, SemanticModel, MemberReferenceType);

        base.VisitAssignmentExpression(node);
    }

    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        Analyzer.AnalyzeLocalDeclaration(SourceElement, node, SemanticModel);
        base.VisitLocalDeclarationStatement(node);
    }

    /// <summary>
    ///     typeof(Foo)
    /// </summary>
    public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);

        // Don't get down to the identifier
        //base.VisitTypeOfExpression(node);
    }

    /// <summary>
    ///     sizeof(Foo)
    /// </summary>
    public override void VisitSizeOfExpression(SizeOfExpressionSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitSizeOfExpression(node);
    }

    /// <summary>
    ///     default(Foo)
    /// </summary>
    public override void VisitDefaultExpression(DefaultExpressionSyntax node)
    {
        // default(Foo) - Uses relationship to type
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitDefaultExpression(node);
    }

    /// <summary>
    ///     var x = (Foo)y
    /// </summary>
    public override void VisitCastExpression(CastExpressionSyntax node)
    {
        // (Foo)obj - Uses relationship to cast type
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);

        // A cast may invoke a user-defined conversion operator ("(double)celsius" -> op_Explicit).
        Analyzer.AnalyzeOperatorUsage(SourceElement, node, SemanticModel, MemberReferenceType);

        base.VisitCastExpression(node);
    }

    /// <summary>
    ///     var x = y as Foo
    ///     var x = y is Foo
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
                Analyzer.AddTypeRelationship(SourceElement, typeInfo.Type, RelationshipType.Uses, location);
            }
        }
        else
        {
            // "a + b", "a == b", ... may bind to a user-defined operator (op_Addition, op_Equality, ...).
            Analyzer.AnalyzeOperatorUsage(SourceElement, node, SemanticModel, MemberReferenceType);
        }

        base.VisitBinaryExpression(node);
    }

    /// <summary>
    ///     "-a", "!a", "~a", "++a" may bind to a user-defined operator (op_UnaryNegation, ...).
    /// </summary>
    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        Analyzer.AnalyzeOperatorUsage(SourceElement, node, SemanticModel, MemberReferenceType);
        base.VisitPrefixUnaryExpression(node);
    }

    /// <summary>
    ///     "a++", "a--" may bind to a user-defined operator (op_Increment / op_Decrement).
    /// </summary>
    public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        Analyzer.AnalyzeOperatorUsage(SourceElement, node, SemanticModel, MemberReferenceType);
        base.VisitPostfixUnaryExpression(node);
    }

    /// <summary>
    ///     Initializers ("Celsius c = 21.5;") may apply an implicit user-defined conversion to the value.
    /// </summary>
    public override void VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        Analyzer.AnalyzeImplicitConversion(SourceElement, node.Value, SemanticModel, MemberReferenceType);
        base.VisitEqualsValueClause(node);
    }

    /// <summary>
    ///     "return 21.5;" in a Celsius-returning method applies an implicit user-defined conversion.
    /// </summary>
    public override void VisitReturnStatement(ReturnStatementSyntax node)
    {
        if (node.Expression is not null)
        {
            Analyzer.AnalyzeImplicitConversion(SourceElement, node.Expression, SemanticModel, MemberReferenceType);
        }

        base.VisitReturnStatement(node);
    }

    /// <summary>
    ///     "Take(21.5)" with Take(Celsius) applies an implicit user-defined conversion to the argument.
    /// </summary>
    public override void VisitArgument(ArgumentSyntax node)
    {
        Analyzer.AnalyzeImplicitConversion(SourceElement, node.Expression, SemanticModel, MemberReferenceType);
        base.VisitArgument(node);
    }

    /// <summary>
    ///     Expression bodies ("=> 21.5") may apply an implicit user-defined conversion to the result.
    /// </summary>
    public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
    {
        Analyzer.AnalyzeImplicitConversion(SourceElement, node.Expression, SemanticModel, MemberReferenceType);
        base.VisitArrowExpressionClause(node);
    }

    /// <summary>
    ///     Declaration pattern: obj is Foo f / case Foo f:
    /// </summary>
    public override void VisitDeclarationPattern(DeclarationPatternSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitDeclarationPattern(node);
    }

    /// <summary>
    ///     Type pattern: nested patterns like "is Foo or Bar".
    ///     (The classic top-level "obj is Foo" stays a BinaryExpression, handled above.)
    /// </summary>
    public override void VisitTypePattern(TypePatternSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitTypePattern(node);
    }

    /// <summary>
    ///     A bare type name used as a switch arm ("Square => ..") parses as a constant pattern, not a
    ///     type pattern. We record it only when the expression actually resolves to a type, so real
    ///     constants and enum members (is 5, is Color.Red) are left to normal traversal.
    /// </summary>
    public override void VisitConstantPattern(ConstantPatternSyntax node)
    {
        if (SemanticModel.GetSymbolInfo(node.Expression).Symbol is ITypeSymbol &&
            node.Expression is TypeSyntax typeSyntax)
        {
            Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, typeSyntax);
        }

        base.VisitConstantPattern(node);
    }

    /// <summary>
    ///     Recursive pattern: obj is Foo { Prop: ... }. The leading type is optional.
    /// </summary>
    public override void VisitRecursivePattern(RecursivePatternSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitRecursivePattern(node);
    }

    /// <summary>
    ///     catch (Foo ex) — the caught exception type. The identifier is optional.
    /// </summary>
    public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        base.VisitCatchDeclaration(node);
    }

    /// <summary>
    ///     foreach (Foo item in ...) — the iteration variable type, plus the implicit
    ///     GetEnumerator/GetAsyncEnumerator call of the enumeration pattern.
    /// </summary>
    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Type);
        Analyzer.AnalyzeForEachStatement(SourceElement, node, SemanticModel, MemberReferenceType);
        base.VisitForEachStatement(node);
    }

    /// <summary>
    ///     foreach (var (x, y) in pairs) — the deconstructing form: GetEnumerator plus the per-element
    ///     Deconstruct call.
    /// </summary>
    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        Analyzer.AnalyzeForEachStatement(SourceElement, node, SemanticModel, MemberReferenceType);
        base.VisitForEachVariableStatement(node);
    }

    /// <summary>
    ///     using (Foo x = ...) statement form. The "using var x = ..." declaration form is a
    ///     LocalDeclarationStatementSyntax and is already handled by AnalyzeLocalDeclaration.
    /// </summary>
    public override void VisitUsingStatement(UsingStatementSyntax node)
    {
        if (node.Declaration != null)
        {
            Analyzer.AnalyzeTypeSyntax(SourceElement, SemanticModel, node.Declaration.Type);
        }

        base.VisitUsingStatement(node);
    }

    /// <summary>
    ///     new Foo[n] — the array element type. ArrayCreationExpressionSyntax is not a
    ///     BaseObjectCreationExpressionSyntax, so the object-creation handling never sees it.
    ///     AddTypeRelationship resolves the IArrayTypeSymbol down to its element type as Uses.
    /// </summary>
    public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
    {
        var typeInfo = SemanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            Analyzer.AddTypeRelationship(SourceElement, typeInfo.Type, RelationshipType.Uses, node.GetSyntaxLocation());
        }

        base.VisitArrayCreationExpression(node);
    }

    /// <summary>
    ///     stackalloc Foo[n] — like array creation, the element type is the dependency. The expression
    ///     type is Span&lt;Foo&gt; (or Foo*), which AddTypeRelationship resolves down to Foo.
    /// </summary>
    public override void VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
    {
        var typeInfo = SemanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            Analyzer.AddTypeRelationship(SourceElement, typeInfo.Type, RelationshipType.Uses, node.GetSyntaxLocation());
        }

        base.VisitStackAllocArrayCreationExpression(node);
    }

    /// <summary>
    ///     stackalloc[] { a, b } — the implicit form; the element type is inferred.
    /// </summary>
    public override void VisitImplicitStackAllocArrayCreationExpression(ImplicitStackAllocArrayCreationExpressionSyntax node)
    {
        var typeInfo = SemanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            Analyzer.AddTypeRelationship(SourceElement, typeInfo.Type, RelationshipType.Uses, node.GetSyntaxLocation());
        }

        base.VisitImplicitStackAllocArrayCreationExpression(node);
    }
}