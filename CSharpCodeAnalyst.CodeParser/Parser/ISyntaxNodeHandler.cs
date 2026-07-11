using CSharpCodeAnalyst.CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Methods the syntax walkers call to report findings.
/// </summary>
public interface ISyntaxNodeHandler
{
    /// <summary>
    ///     Analyzes a method invocation. In a method body (default, "Calls") the edge carries the
    ///     call-style attributes (static/instance/base/...) and event invocations ("MyEvent?.Invoke()")
    ///     are detected. In a lambda body ("Uses") the invocation is deferred: no call-style attributes
    ///     (except the extension-method marker) and no event-invoke detection - referencing an event in
    ///     a lambda does not assert that it fires.
    /// </summary>
    void AnalyzeInvocation(CodeElement sourceElement, InvocationExpressionSyntax invocationSyntax,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls);

    /// <summary>
    ///     Analyzes assignment expressions for event registration/unregistration.
    ///     Property/field access on left and right sides is handled by the walker's normal traversal.
    /// </summary>
    void AnalyzeEventRegistrationAssignment(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
        SemanticModel semanticModel);

    /// <summary>
    ///     Analyzes constructor chaining (": base(...)" and ": this(...)").
    ///     A ConstructorInitializerSyntax is not an InvocationExpressionSyntax, so it needs its own
    ///     handler to create the "Calls" relationship to the chained constructor.
    /// </summary>
    void AnalyzeConstructorInitializer(CodeElement sourceElement, ConstructorInitializerSyntax initializerSyntax,
        SemanticModel semanticModel);

    /// <summary>
    ///     Analyzes standalone simple-name references (fields, properties, method groups, etc.).
    ///     Covers both plain identifiers (IdentifierNameSyntax) and generic names (GenericNameSyntax,
    ///     e.g. the method group "Create&lt;Widget&gt;") - hence the SimpleNameSyntax parameter.
    ///     Ownership: Handles ONLY standalone names. Names that are part of MemberAccessExpressions are
    ///     NOT visited here - they're handled by AnalyzeMemberAccess.
    ///     The propertyAccessType parameter controls whether property access creates "Calls" or "Uses" relationships.
    ///     Default is "Calls" for method bodies; lambda bodies should pass "Uses" because we don't know when/if the lambda
    ///     executes.
    /// </summary>
    void AnalyzeIdentifier(CodeElement sourceElement, SimpleNameSyntax identifierSyntax,
        SemanticModel semanticModel, RelationshipType propertyAccessType = RelationshipType.Calls);

    /// <summary>
    ///     Analyzes member access expressions (obj.Property, obj.Field, obj.Event).
    ///     Ownership: Handles the member being accessed (the .Name part on the right side).
    ///     The Expression (left side) is handled by the walker, which will visit it independently.
    ///     The propertyAccessType parameter controls whether property access creates "Calls" or "Uses" relationships.
    ///     Default is "Calls" for method bodies; lambda bodies should pass "Uses" because we don't know when/if the lambda
    ///     executes.
    /// </summary>
    void AnalyzeMemberAccess(CodeElement sourceElement, MemberAccessExpressionSyntax memberAccessSyntax,
        SemanticModel semanticModel, RelationshipType propertyAccessType = RelationshipType.Calls);

    /// <summary>
    ///     Analyzes element access expressions that resolve to an indexer ("store[key]"). Covers both the
    ///     direct form (ElementAccessExpressionSyntax) and the conditional form "store?[key]"
    ///     (ElementBindingExpressionSyntax) - hence the common ExpressionSyntax parameter. Array element
    ///     access yields no indexer symbol and is ignored. Like AnalyzeMemberAccess, the receiver and the
    ///     argument expressions are handled by the walker's normal traversal.
    /// </summary>
    void AnalyzeElementAccess(CodeElement sourceElement, ExpressionSyntax elementAccessSyntax,
        SemanticModel semanticModel, RelationshipType propertyAccessType = RelationshipType.Calls);

    void AnalyzeLocalDeclaration(CodeElement sourceElement, LocalDeclarationStatementSyntax localDeclaration,
        SemanticModel semanticModel);

    /// <summary>
    ///     new() is ImplicitObjectCreationExpressionSyntax.
    ///     new Class() is ObjectCreationExpressionSyntax
    ///     They are different cases in the MethodBodyWalker,
    ///     but both expressions derive from BaseObjectCreationExpressionSyntax
    /// 
    ///     If the object is created as part of a field initialization additional steps are necessary
    ///     - Source for the creates relationship is the containing class and not to the field.
    ///     - Constructor calls relationship is omitted.
    ///     - Field gets an uses relationship to the created type (may not be the same as the field type)
    /// </summary>
    void AnalyzeObjectCreation(CodeElement sourceElement, SemanticModel semanticModel,
        BaseObjectCreationExpressionSyntax objectCreationSyntax, bool isFieldInitializer);

    /// <summary>
    ///     Adds a relationship to a type the walker has already resolved itself. Used by the visits that
    ///     work with type info instead of a syntax name: is/as expressions, array and stackalloc
    ///     creations, object creations in lambda bodies.
    /// </summary>
    void AddTypeRelationship(CodeElement sourceElement, ITypeSymbol typeSymbol,
        RelationshipType relationshipType,
        SourceLocation? location = null);

    /// <summary>
    ///     Records a "Uses" relationship to the constructor referenced by an object creation inside a lambda.
    ///     The lambda references the constructor but does not invoke it now (we do not know when the lambda
    ///     runs), so this is "Uses" rather than "Calls"/"Creates". For access from LambdaBodyWalker.
    /// </summary>
    void AddConstructorReferenceFromLambda(CodeElement sourceElement,
        BaseObjectCreationExpressionSyntax objectCreationSyntax, SemanticModel semanticModel);

    /// <summary>
    ///     typeof(),
    ///     sizeof()
    ///     (cast)
    /// </summary>
    void AnalyzeTypeSyntax(CodeElement sourceElement, SemanticModel semanticModel, TypeSyntax? node);

    /// <summary>
    ///     Analyzes an expression whose bound symbol may be a user-defined operator or conversion method:
    ///     binary/unary expressions ("a + b", "-a"), compound assignments ("a += b") and explicit casts
    ///     ("(double)c"). Built-in operators bind to no user-defined method and are ignored.
    /// </summary>
    void AnalyzeOperatorUsage(CodeElement sourceElement, ExpressionSyntax expression,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls);

    /// <summary>
    ///     Analyzes the implicit conversion applied to an expression in its context ("Celsius c = 21.5;").
    ///     Only user-defined conversions (op_Implicit) create an edge; identity, numeric and reference
    ///     conversions have no method to point at. Called by the walkers at the positions where implicit
    ///     conversions occur: initializers, assignment right sides, return values, arguments, arrow bodies.
    /// </summary>
    void AnalyzeImplicitConversion(CodeElement sourceElement, ExpressionSyntax expression,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls);

    /// <summary>
    ///     Analyzes a LINQ query expression ("from x in xs where P(x) select F(x)"). Records the implicit
    ///     query-pattern method calls the compiler synthesizes from the clauses (Where/Select/OrderBy/
    ///     Join/Cast/...) - only for this query; sub-queries nested in clause expressions are reached by
    ///     the lambda walker. The clause expressions themselves are NOT walked here: the walkers route
    ///     them through the lambda semantics, because the compiler turns them into lambdas.
    /// </summary>
    void AnalyzeQueryExpression(CodeElement sourceElement, QueryExpressionSyntax querySyntax,
        SemanticModel semanticModel, RelationshipType operatorCallType = RelationshipType.Calls);

    /// <summary>
    ///     Analyzes a deconstructing assignment ("var (x, y) = point;"). The compiler calls the
    ///     user-defined Deconstruct method, which never appears as an invocation in the syntax tree.
    ///     Pure tuple deconstructions ("(a, b) = (b, a)") bind no method and produce no edge.
    /// </summary>
    void AnalyzeDeconstruction(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls);

    /// <summary>
    ///     Analyzes the enumeration pattern of a foreach statement. The compiler calls GetEnumerator
    ///     (or GetAsyncEnumerator for "await foreach"), which never appears as an invocation in the
    ///     syntax tree. Only the pattern entry point gets an edge - MoveNext/Current live on the
    ///     enumerator type and would be noise. Covers the deconstructing form
    ///     ("foreach (var (x, y) in pairs)") as well.
    /// </summary>
    void AnalyzeForEachStatement(CodeElement sourceElement, CommonForEachStatementSyntax forEachSyntax,
        SemanticModel semanticModel, RelationshipType relationshipType = RelationshipType.Calls);
}