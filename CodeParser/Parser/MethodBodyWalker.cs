using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser
{

    /// <summary>
    ///     Syntax walker for analyzing method and property bodies.
    ///     This approach simplifies the analysis by focusing on specific syntax nodes.
    /// </summary>
    internal class MethodBodyWalker : CSharpSyntaxWalker
    {
        private readonly RelationshipAnalyzer _analyzer;
        private readonly CodeElement _sourceElement;
        private readonly SemanticModel _semanticModel;
        private readonly bool _isFieldInitializer;

        public MethodBodyWalker(RelationshipAnalyzer analyzer, CodeElement sourceElement, SemanticModel semanticModel, bool isFieldInitializer)
        {
            _analyzer = analyzer;
            _sourceElement = sourceElement;
            _semanticModel = semanticModel;
            _isFieldInitializer = isFieldInitializer;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            _analyzer.AnalyzeInvocation(_sourceElement, node, _semanticModel);
            // Note: We still call base to visit arguments, but AnalyzeInvocation won't re-process them
            base.VisitInvocationExpression(node);
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            _analyzer.AnalyzeAssignment(_sourceElement, node, _semanticModel);
            base.VisitAssignmentExpression(node);
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            _analyzer.AnalyzeIdentifier(_sourceElement, node, _semanticModel);
            base.VisitIdentifierName(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            _analyzer.AnalyzeMemberAccess(_sourceElement, node, _semanticModel);

            // Explicitly visit only the Expression (left side: obj in obj.Property)
            // The Name (right side: Property) is already handled by AnalyzeMemberAccess
            // This gives clear ownership: MemberAccess owns the .Name, walker handles .Expression independently
            Visit(node.Expression);
        }

        public override void VisitArgument(ArgumentSyntax node)
        {
            _analyzer.AnalyzeArgument(_sourceElement, node, _semanticModel);
            base.VisitArgument(node);
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            _analyzer.AnalyzeLocalDeclaration(_sourceElement, node, _semanticModel);
            base.VisitLocalDeclarationStatement(node);
        }
        
        /// <summary>
        /// new() is ImplicitObjectCreationExpressionSyntax. So ObjectCreationExpressionSyntax does not detect it.
        /// </summary>
        public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
        {
            _analyzer.AnalyzeObjectCreation(_sourceElement, _semanticModel, node, _isFieldInitializer);
            base.VisitImplicitObjectCreationExpression(node);
        }
        
        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            _analyzer.AnalyzeObjectCreation(_sourceElement, _semanticModel, node, _isFieldInitializer);
            base.VisitObjectCreationExpression(node);
        }
    }
}
