using CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Methods the syntax walkers call to report findings.
/// </summary>
public interface ISyntaxNodeHandler
{
    void AnalyzeInvocation(CodeElement sourceElement, InvocationExpressionSyntax invocationSyntax,
        SemanticModel semanticModel);

    /// <summary>
    ///     Analyzes assignment expressions for event registration/unregistration.
    ///     Property/field access on left and right sides is handled by the walker's normal traversal.
    /// </summary>
    void AnalyzeEventRegistrationAssignment(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
        SemanticModel semanticModel);

    /// <summary>
    ///     Analyzes standalone identifier references (fields, properties, etc.).
    ///     Ownership: Handles ONLY standalone identifiers. Identifiers that are part of
    ///     MemberAccessExpressions are NOT visited here - they're handled by AnalyzeMemberAccess.
    ///     The propertyAccessType parameter controls whether property access creates "Calls" or "Uses" relationships.
    ///     Default is "Calls" for method bodies; lambda bodies should pass "Uses" because we don't know when/if the lambda
    ///     executes.
    /// </summary>
    void AnalyzeIdentifier(CodeElement sourceElement, IdentifierNameSyntax identifierSyntax,
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

    void AnalyzeArgument(CodeElement sourceElement, ArgumentSyntax argumentSyntax, SemanticModel semanticModel);

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
    ///     Public wrapper for AddTypeRelationship to allow access from LambdaBodyWalker
    /// </summary>
    void AddTypeRelationshipPublic(CodeElement sourceElement, ITypeSymbol typeSymbol,
        RelationshipType relationshipType,
        SourceLocation? location = null);

    /// <summary>
    ///     Public wrapper for AddRelationshipWithFallbackToContainingType to allow access from LambdaBodyWalker.
    ///     Adds a relationship to a symbol (method, property, field, event), with fallback to containing type for external
    ///     symbols.
    /// </summary>
    void AddSymbolRelationshipPublic(CodeElement sourceElement, ISymbol targetSymbol,
        RelationshipType relationshipType, List<SourceLocation>? locations, RelationshipAttribute attributes);

    /// <summary>
    ///     typeof(),
    ///     sizeof()
    ///     (cast)
    /// </summary>
    void AnalyzeTypeSyntax(CodeElement sourceElement, SemanticModel semanticModel, TypeSyntax? node);
}