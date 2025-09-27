using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Services;
using CSharpCodeAnalyst.Shared.TabularData;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

public class RuleViolationViewModel : TableRow
{
    private readonly CodeGraph _codeGraph;
    private readonly Violation _violation;

    public RuleViolationViewModel(Violation violation, CodeGraph codeGraph)
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

            if (sourceElement != null && targetElement != null)
            {
                var detailViewModel = new RelationshipViewModel(relationship, sourceElement, targetElement);
                details.Add(detailViewModel);
            }
        }

        return details;
    }

    private void OnOpenSourceLocation(RelationshipViewModel? detailViewModel)
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