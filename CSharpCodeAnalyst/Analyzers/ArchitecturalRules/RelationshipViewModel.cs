using System.Windows;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Services;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public class RelationshipViewModel
{
    private readonly Relationship _relationship;
    private readonly CodeElement _sourceElement;
    private readonly CodeElement _targetElement;

    public RelationshipViewModel(Relationship relationship, CodeElement sourceElement, CodeElement targetElement)
    {
        _relationship = relationship;
        _sourceElement = sourceElement;
        _targetElement = targetElement;

        Description = $"{_sourceElement.FullName} → {_targetElement.FullName}";
        OpenSourceLocationCommand = new WpfCommand<SourceLocation>(OnOpenSourceLocation);

        // Try to get a source location from the relationship or source element
        SourceLocation = GetBestSourceLocation();
    }

    public string Description { get; }
    public ICommand OpenSourceLocationCommand { get; }
    public SourceLocation? SourceLocation { get; }

    public bool HasSourceLocation
    {
        get => SourceLocation != null;
    }

    private SourceLocation? GetBestSourceLocation()
    {
        // First try relationship source locations
        if (_relationship.SourceLocations?.Any() == true)
        {
            return _relationship.SourceLocations.First();
        }

        // Fall back to source element locations
        if (_sourceElement.SourceLocations?.Any() == true)
        {
            return _sourceElement.SourceLocations.First();
        }

        return null;
    }

    private static void OnOpenSourceLocation(SourceLocation? location)
    {
        if (location is null)
        {
            return;
        }

        try
        {
            var fileOpener = new FileOpener();
            fileOpener.TryOpenFile(location.File, location.Line, location.Column);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}