using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

public partial class Parser
{
    /// <summary>
    ///     Properties became quite complex.
    ///     We treat the property like a method and do not distinguish between getter and setter.
    ///     A property can have a getter, setter or an expression body.
    /// </summary>
    private void AnalyzePropertyDependencies(Solution solution, CodeElement propertyElement,
        IPropertySymbol propertySymbol)
    {
        // Analyze the property type
        AddTypeDependency(propertyElement, propertySymbol.Type, DependencyType.Uses);

        // Check for interface implementation
        var implementedInterfaceProperty = GetImplementedInterfaceProperty(propertySymbol);
        if (implementedInterfaceProperty != null)
        {
            var locations = GetLocations(propertySymbol);
            AddPropertyDependency(propertyElement, implementedInterfaceProperty, DependencyType.Implements, locations);
        }

        // Check for property override
        if (propertySymbol.IsOverride)
        {
            var overriddenProperty = propertySymbol.OverriddenProperty;
            if (overriddenProperty != null)
            {
                var locations = GetLocations(propertySymbol);
                AddPropertyDependency(propertyElement, overriddenProperty, DependencyType.Overrides, locations);
            }
        }

        // Analyze the property body (including accessors)
        AnalyzePropertyBody(solution, propertyElement, propertySymbol);
    }

    private void AnalyzePropertyBody(Solution solution, CodeElement propertyElement, IPropertySymbol propertySymbol)
    {
        foreach (var syntaxReference in propertySymbol.DeclaringSyntaxReferences)
        {
            var syntax = syntaxReference.GetSyntax();
            if (syntax is PropertyDeclarationSyntax propertyDeclaration)
            {
                var document = solution.GetDocument(syntax.SyntaxTree);
                var semanticModel = document?.GetSemanticModelAsync().Result;
                if (semanticModel != null)
                {
                    if (propertyDeclaration.ExpressionBody != null)
                    {
                        AnalyzeMethodBody(propertyElement, propertyDeclaration.ExpressionBody.Expression,
                            semanticModel);
                    }
                    else if (propertyDeclaration.AccessorList != null)
                    {
                        foreach (var accessor in propertyDeclaration.AccessorList.Accessors)
                        {
                            if (accessor.ExpressionBody != null)
                            {
                                AnalyzeMethodBody(propertyElement, accessor.ExpressionBody.Expression, semanticModel);
                            }
                            else if (accessor.Body != null)
                            {
                                AnalyzeMethodBody(propertyElement, accessor.Body, semanticModel);
                            }
                        }
                    }
                }
            }
        }
    }

    private void AddPropertyDependency(CodeElement sourceElement, IPropertySymbol propertySymbol,
        DependencyType dependencyType, List<SourceLocation> locations)
    {
        AddDependencyWithFallbackToContainingType(sourceElement, propertySymbol, dependencyType, locations);
    }

    private IPropertySymbol? GetImplementedInterfaceProperty(IPropertySymbol propertySymbol)
    {
        var containingType = propertySymbol.ContainingType;
        foreach (var @interface in containingType.AllInterfaces)
        {
            var interfaceMembers = @interface.GetMembers().OfType<IPropertySymbol>();
            foreach (var interfaceProperty in interfaceMembers)
            {
                var implementingProperty = containingType.FindImplementationForInterfaceMember(interfaceProperty);
                if (implementingProperty != null && implementingProperty.Equals(propertySymbol))
                {
                    return interfaceProperty;
                }
            }
        }

        return null;
    }
}