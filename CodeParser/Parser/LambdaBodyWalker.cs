using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Specialized walker for lambda/anonymous method bodies.
///     Tracks type relationships (object creation, variable declarations) and method/member references
///     using "Uses" relationships (not "Calls" or "Creates").
///     This reflects the fact that we know what types and members are referenced to define the lambda,
///     but we don't know when/if the lambda will execute (hence "Uses" instead of "Calls").
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
        TrackObjectCreationAsUses(node);
        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        TrackObjectCreationAsUses(node);
        base.VisitObjectCreationExpression(node);
    }

    /// <summary>
    ///     Tracks object creation with "Uses" relationship (not "Creates" for lambdas).
    ///     Handles both implicit (new()) and explicit (new Foo()) object creation.
    /// </summary>
    private void TrackObjectCreationAsUses(BaseObjectCreationExpressionSyntax node)
    {
        var typeInfo = _semanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            var location = node.GetSyntaxLocation();
            _analyzer.AddTypeRelationshipPublic(_sourceElement, typeInfo.Type, RelationshipType.Uses, location);
        }
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Track method references with "Uses" relationship (not "Calls")
        // We don't know when the lambda executes, but we know it references these methods
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IMethodSymbol calledMethod)
        {
            // Skip local functions - they should not be part of the dependency graph
            if (calledMethod.MethodKind == MethodKind.LocalFunction)
            {
                return;
            }

            var location = node.GetSyntaxLocation();

            // Add "Uses" relationship to the method (with fallback to containing type)
            _analyzer.AddSymbolRelationshipPublic(
                _sourceElement,
                calledMethod,
                RelationshipType.Uses,
                [location],
                RelationshipAttribute.None);

            // Handle generic method invocations - track type arguments
            if (calledMethod.IsGenericMethod)
            {
                foreach (var typeArg in calledMethod.TypeArguments)
                {
                    _analyzer.AddTypeRelationshipPublic(_sourceElement, typeArg, RelationshipType.Uses, location);
                }
            }
        }

        // Don't call base - we don't want to descend into arguments or track other relationships
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
        // Track member references (properties, fields, events) with "Uses" relationship
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        var symbol = symbolInfo.Symbol;
        var location = node.GetSyntaxLocation();

        if (symbol is IPropertySymbol propertySymbol)
        {
            _analyzer.AddSymbolRelationshipPublic(
                _sourceElement, propertySymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            _analyzer.AddSymbolRelationshipPublic(
                _sourceElement, fieldSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }
        else if (symbol is IEventSymbol eventSymbol)
        {
            _analyzer.AddSymbolRelationshipPublic(
                _sourceElement, eventSymbol, RelationshipType.Uses, [location], RelationshipAttribute.None);
        }

        // Don't call base - we don't want to descend further
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