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

/// <summary>
/// Single row in the architectural rule result table.
/// </summary>
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

            // The participants of the cycle - the same count the Cycles view shows as
            // "Element Count", so the group can be identified there.
            NoCyclesRule => _violation.CycleElements.Count,
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
            NoCyclesRule noCyclesRule => noCyclesRule.Source.Length > 0
                ? noCyclesRule.Source
                : Strings.ArchitecturalRules_Scope_Everything,

            // A group merges rules with overlapping sources; show every distinct source pattern.
            RestrictRuleGroup restrictRuleGroup => string.Join("\n", restrictRuleGroup.Sources),
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

            // The cycle group's name - the Cycles view shows the group under the same name, so
            // the user can find it there and analyze it further.
            NoCyclesRule => _violation.CycleName ?? "(cycle)",
            MetricRule metricRule =>
                string.Format(Strings.ArchitecturalRules_Metric_Max, metricRule.FormatValue(metricRule.Threshold)),
            _ => ""
        };
    }

    /// <summary>
    /// Expandable row details. Source locations of the violated rule.
    /// </summary>
    private ObservableCollection<ViolationDetailViewModel> CreateDetails()
    {
        return _violation.Rule switch
        {
            CodeElementMetricRule elementMetricRule => CreateDetailsForCodeElementMetricRule(elementMetricRule),
            NoCyclesRule => CreateDetailsForNoCyclesRule(),
            _ => CreateDetailsForViolatingRelationships()
        };
    }

    private ObservableCollection<ViolationDetailViewModel> CreateDetailsForViolatingRelationships()
    {
        var details = new ObservableCollection<ViolationDetailViewModel>();
        foreach (var relationship in _violation.ViolatingRelationships)
        {
            var sourceElement = _codeGraph.Nodes.GetValueOrDefault(relationship.SourceId);
            var targetElement = _codeGraph.Nodes.GetValueOrDefault(relationship.TargetId);

            // It can have multiple source locations.
            if (sourceElement != null && targetElement != null)
            {
                if (relationship.SourceLocations.Count <= 1)
                {
                    var sourceLocation = relationship.SourceLocations.Any()
                        ? relationship.SourceLocations.Single()
                        : sourceElement.SourceLocations.FirstOrDefault();

                    var detailViewModel = new ViolationDetailViewModel(sourceElement, targetElement, sourceLocation);
                    details.Add(detailViewModel);
                }
                else
                {
                    // Number the locations if there are multiple.
                    for (var index = 0; index < relationship.SourceLocations.Count; index++)
                    {
                        var location = relationship.SourceLocations[index];
                        var detailViewModel = new ViolationDetailViewModel(sourceElement, targetElement, location, index + 1);
                        details.Add(detailViewModel);
                    }
                }
            }
        }

        return details;
    }

    private ObservableCollection<ViolationDetailViewModel> CreateDetailsForNoCyclesRule()
    {
        var details = new ObservableCollection<ViolationDetailViewModel>();
        // The cycle participants, not the single edges: name and count identify the group
        // in the Cycles view, which is the place to analyze the cycle further.
        foreach (var element in _violation.CycleElements)
        {
            details.Add(new ViolationDetailViewModel(element));
        }

        return details;
    }

    private ObservableCollection<ViolationDetailViewModel> CreateDetailsForCodeElementMetricRule(CodeElementMetricRule elementMetricRule)
    {
        var details = new ObservableCollection<ViolationDetailViewModel>();
        foreach (var (element, value) in _violation.ViolatingElements)
        {
            details.Add(new ViolationDetailViewModel(element, elementMetricRule.FormatValue(value)));
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