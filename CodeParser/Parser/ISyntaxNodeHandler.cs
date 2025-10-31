using Contracts.Graph;
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

    void AnalyzeAssignment(CodeElement sourceElement, AssignmentExpressionSyntax assignmentExpression,
        SemanticModel semanticModel);

    /// <summary>
    ///     Analyzes standalone identifier references (fields, properties, etc.).
    ///     Ownership: Handles ONLY standalone identifiers. Identifiers that are part of
    ///     MemberAccessExpressions are NOT visited here - they're handled by AnalyzeMemberAccess.
    /// </summary>
    void AnalyzeIdentifier(CodeElement sourceElement, IdentifierNameSyntax identifierSyntax,
        SemanticModel semanticModel);

    /// <summary>
    ///     Analyzes member access expressions (obj.Property, obj.Field, obj.Event).
    ///     Ownership: Handles the member being accessed (the .Name part on the right side).
    ///     The Expression (left side) is handled by the walker, which will visit it independently.
    /// </summary>
    void AnalyzeMemberAccess(CodeElement sourceElement, MemberAccessExpressionSyntax memberAccessSyntax,
        SemanticModel semanticModel);

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
    ///     Adds a relationship to a symbol (method, property, field, event), with fallback to containing type for external symbols.
    /// </summary>
    void AddSymbolRelationshipPublic(CodeElement sourceElement, ISymbol targetSymbol,
        RelationshipType relationshipType, List<SourceLocation>? locations, RelationshipAttribute attributes);

    /// <summary>
    ///     Public wrapper for AddEventUsageRelationship to allow access from LambdaBodyWalker
    /// </summary>
    void AddEventUsageRelationshipPublic(CodeElement sourceElement, IEventSymbol eventSymbol,
        SourceLocation location, RelationshipAttribute attribute = RelationshipAttribute.None);

    /// <summary>
    ///     Public wrapper for AddEventHandlerRelationship to allow access from LambdaBodyWalker
    /// </summary>
    void AddEventHandlerRelationshipPublic(IMethodSymbol handlerMethod, IEventSymbol eventSymbol,
        SourceLocation location, RelationshipAttribute attribute);

    /// <summary>
    /// typeof(),
    /// sizeof()
    /// (cast)
    /// </summary>
    void AnalyzeTypeSyntax(CodeElement sourceElement, SemanticModel semanticModel, TypeSyntax? node);
}