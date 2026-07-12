using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

/// <summary>
///     A line in the detail view of a violation, either a violating relationship or a violating code
///     element. Note that a view model is created for each source location of a relationship.
/// </summary>
public class ViolationDetailViewModel
{
    public ViolationDetailViewModel(CodeElement sourceElement, CodeElement targetElement, SourceLocation? source, int number = 0)
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

    /// <summary>A code element that broke the threshold of a metric rule, annotated with its value.</summary>
    public ViolationDetailViewModel(CodeElement element, string formattedValue)
    {
        SourceLocation = element.SourceLocations.FirstOrDefault();
        Description = $"{element.FullName} ({formattedValue})";
    }

    /// <summary>A code element participating in a dependency cycle.</summary>
    public ViolationDetailViewModel(CodeElement element)
    {
        SourceLocation = element.SourceLocations.FirstOrDefault();
        Description = element.FullName;
    }

    public string Description { get; }
    public SourceLocation? SourceLocation { get; }
}
