using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

/// <summary>
///     A line in the detail view. Note a view model is created for each source location in a relationship.
/// </summary>
public class RelationshipViewModel
{
    public RelationshipViewModel(CodeElement sourceElement, CodeElement targetElement, SourceLocation? source, int number = 0)
    {
        SourceLocation = source ?? sourceElement.SourceLocations.FirstOrDefault();
        if (number > 0)
        {
            Description = $"{sourceElement.FullName} → {targetElement.FullName} ({number})";
        }
        else
        {
            Description = $"{sourceElement.FullName} → {targetElement.FullName}";
        }
    }

    public string Description { get; }
    public SourceLocation? SourceLocation { get; }
}