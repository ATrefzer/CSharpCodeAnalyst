using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.Shared.Services;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

public class RuleViolationViewModel : TableRow
{
    private readonly CodeGraph.Graph.CodeGraph _codeGraph;
    private readonly Violation _violation;

    public RuleViolationViewModel(Violation violation, CodeGraph.Graph.CodeGraph codeGraph)
    {
        ErrorIcon = IconLoader.LoadIcon("Resources/error.png");
        _violation = violation;
        _codeGraph = codeGraph;

        // Table columns
        RuleType = GetRuleTypeDisplayName();
        Source = _violation.Rule.Source;
        Target = GetTargetDisplayValue();
        ViolationCount = _violation.ViolatingRelationships.Count;

        // Detail relationships
        RelationshipDetails = CreateRelationshipDetails();
        OpenSourceLocationCommand = new WpfCommand<RelationshipViewModel>(OnOpenSourceLocation);
    }

    public ImageSource? ErrorIcon { get; set; }

    // Table columns
    public string RuleType { get; }
    public string Source { get; }
    public string Target { get; }
    public int ViolationCount { get; }

    // Detail data
    public ObservableCollection<RelationshipViewModel> RelationshipDetails { get; }
    public ICommand OpenSourceLocationCommand { get; }

    private string GetRuleTypeDisplayName()
    {
        return _violation.Rule.GetType().Name.Replace("Rule", "").ToUpper();
    }

    private string GetTargetDisplayValue()
    {
        return _violation.Rule switch
        {
            DenyRule denyRule => denyRule.Target,
            RestrictRule restrictRule => restrictRule.Target,
            IsolateRule => "(isolated)",
            _ => ""
        };
    }

    private ObservableCollection<RelationshipViewModel> CreateRelationshipDetails()
    {
        var details = new ObservableCollection<RelationshipViewModel>();

        foreach (var relationship in _violation.ViolatingRelationships)
        {
            var sourceElement = _codeGraph.Nodes.GetValueOrDefault(relationship.SourceId);
            var targetElement = _codeGraph.Nodes.GetValueOrDefault(relationship.TargetId);

            // It can have multiple source locations.
            if (sourceElement != null && targetElement != null)
            {
                RelationshipViewModel detailViewModel;
                if (relationship.SourceLocations.Count <= 1)
                {
                    var sourceLocation = relationship.SourceLocations.Any()
                        ? relationship.SourceLocations.Single()
                        : sourceElement.SourceLocations.FirstOrDefault();

                    detailViewModel = new RelationshipViewModel(sourceElement, targetElement, sourceLocation);
                    details.Add(detailViewModel);
                }
                else
                {
                    // Number the locations if there are multiple.
                    for (var index = 0; index < relationship.SourceLocations.Count; index++)
                    {
                        var location = relationship.SourceLocations[index];
                        detailViewModel = new RelationshipViewModel(sourceElement, targetElement, location, index + 1);
                        details.Add(detailViewModel);
                    }
                }
            }
        }

        return details;
    }

    private static void OnOpenSourceLocation(RelationshipViewModel? detailViewModel)
    {
        if (detailViewModel?.SourceLocation is null)
        {
            return;
        }

        try
        {
            var fileOpener = new FileOpener();
            fileOpener.TryOpenFile(detailViewModel.SourceLocation.File,
                detailViewModel.SourceLocation.Line,
                detailViewModel.SourceLocation.Column);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}