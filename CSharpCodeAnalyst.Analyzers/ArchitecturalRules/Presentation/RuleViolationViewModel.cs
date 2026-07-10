using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Wpf;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

public class RuleViolationViewModel : TableRow
{
    private readonly CodeGraph.Graph.CodeGraph _codeGraph;
    private readonly IPublisher _messaging;
    private readonly Violation _violation;

    public RuleViolationViewModel(Violation violation, CodeGraph.Graph.CodeGraph codeGraph, IPublisher messaging)
    {
        ErrorIcon = IconLoader.LoadIcon(typeof(IconLoader).Assembly.GetName().Name, "Resources/error.png");
        _violation = violation;
        _codeGraph = codeGraph;
        _messaging = messaging;

        // Table columns
        RuleType = GetRuleTypeDisplayName();
        Source = GetSourceDisplayValue();
        Target = GetTargetDisplayValue();
        ViolationCount = GetViolationCount();

        // Detail rows
        Details = CreateDetails();
        OpenSourceLocationCommand = new WpfCommand<ViolationDetailViewModel>(OnOpenSourceLocation);
    }

    public ImageSource? ErrorIcon { get; set; }

    // Table columns
    public string RuleType { get; }
    public string Source { get; }
    public string Target { get; }
    public int ViolationCount { get; }

    // Detail data
    public ObservableCollection<ViolationDetailViewModel> Details { get; }
    public ICommand OpenSourceLocationCommand { get; }

    private string GetRuleTypeDisplayName()
    {
        return _violation.Rule.DisplayName;
    }

    private int GetViolationCount()
    {
        return _violation.Rule switch
        {
            // A system metric rule is either violated or not - there is nothing to count.
            SystemMetricRule => 1,
            CodeElementMetricRule => _violation.ViolatingElements.Count,
            _ => _violation.ViolatingRelationships.Count
        };
    }

    private string GetSourceDisplayValue()
    {
        return _violation.Rule switch
        {
            // A system metric rule has no pattern; show the measured value instead.
            SystemMetricRule systemMetricRule when _violation.MetricValue.HasValue =>
                systemMetricRule.FormatValue(_violation.MetricValue.Value),
            CodeElementMetricRule elementMetricRule => elementMetricRule.Source.Length > 0
                ? elementMetricRule.Source
                : Strings.ArchitecturalRules_Scope_Everything,
            DependencyRule dependencyRule => dependencyRule.Source,
            _ => ""
        };
    }

    private string GetTargetDisplayValue()
    {
        return _violation.Rule switch
        {
            // The whole allowed set, not the target of the first rule of the group.
            RestrictRuleGroup restrictRuleGroup => string.Join("\n", restrictRuleGroup.Targets),
            TargetedDependencyRule targetedRule => targetedRule.Target,
            IsolateRule => "(isolated)",
            MetricRule metricRule =>
                string.Format(Strings.ArchitecturalRules_Metric_Max, metricRule.FormatValue(metricRule.Threshold)),
            _ => ""
        };
    }

    private ObservableCollection<ViolationDetailViewModel> CreateDetails()
    {
        var details = new ObservableCollection<ViolationDetailViewModel>();

        if (_violation.Rule is CodeElementMetricRule elementMetricRule)
        {
            foreach (var (element, value) in _violation.ViolatingElements)
            {
                details.Add(new ViolationDetailViewModel(element, elementMetricRule.FormatValue(value)));
            }

            return details;
        }

        foreach (var relationship in _violation.ViolatingRelationships)
        {
            var sourceElement = _codeGraph.Nodes.GetValueOrDefault(relationship.SourceId);
            var targetElement = _codeGraph.Nodes.GetValueOrDefault(relationship.TargetId);

            // It can have multiple source locations.
            if (sourceElement != null && targetElement != null)
            {
                ViolationDetailViewModel detailViewModel;
                if (relationship.SourceLocations.Count <= 1)
                {
                    var sourceLocation = relationship.SourceLocations.Any()
                        ? relationship.SourceLocations.Single()
                        : sourceElement.SourceLocations.FirstOrDefault();

                    detailViewModel = new ViolationDetailViewModel(sourceElement, targetElement, sourceLocation);
                    details.Add(detailViewModel);
                }
                else
                {
                    // Number the locations if there are multiple.
                    for (var index = 0; index < relationship.SourceLocations.Count; index++)
                    {
                        var location = relationship.SourceLocations[index];
                        detailViewModel = new ViolationDetailViewModel(sourceElement, targetElement, location, index + 1);
                        details.Add(detailViewModel);
                    }
                }
            }
        }

        return details;
    }

    private void OnOpenSourceLocation(ViolationDetailViewModel? detailViewModel)
    {
        if (detailViewModel?.SourceLocation is null)
        {
            return;
        }

        _messaging.Publish(new OpenSourceLocationRequest(detailViewModel.SourceLocation));
    }
}
