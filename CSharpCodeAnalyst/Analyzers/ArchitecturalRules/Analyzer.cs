using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;
    private readonly IMessageBox _messageBox;
    private List<RuleBase> _rules = [];
    private string _rulesText = string.Empty;

    public Analyzer(IPublisher messaging, IMessageBox messageBox)
    {
        _messaging = messaging;
        _messageBox = messageBox;
    }

    /// <summary>
    /// Direct analysis with rules from file (for command-line use)
    /// </summary>
    public List<Violation> Analyze(CodeGraph graph, string fileToRules)
    {
        ParseAndStoreRules(File.ReadAllText(fileToRules));
        return ExecuteAnalysis(graph);
    }

    public void Analyze(CodeGraph graph)
    {
        var dialog = new ArchitecturalRulesDialog
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            // Load existing rules or provide sample rules
            RulesText = string.IsNullOrEmpty(_rulesText) ? GetSampleRules() : _rulesText
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ParseAndStoreRules(dialog.RulesText);
            }
            catch (Exception ex)
            {
                _messageBox.ShowError($"Error parsing rules: {ex.Message}");
                return;
            }


            // Execute analysis
            var violations = ExecuteAnalysis(graph);

            if (violations.Count == 0)
            {
                _messageBox.ShowSuccess("No rule violations found!");
            }
            else
            {
                // Show violations in tabular format
                var violationsViewModel = new RuleViolationsViewModel(violations, graph);
                _messaging.Publish(new ShowTabularDataRequest(violationsViewModel));
            }
        }
    }

    public string Name { get => "Architectural rules"; }
    public string Description { get; } = "Validates your architectural constraints based on user-defined rules";

    public string Id
    {
        get => "ArchitecturalRules";
    }

    public string? GetPersistentData()
    {
        if (string.IsNullOrEmpty(_rulesText))
            return null;

        var persistentData = new PersistenceData
        {
            RulesText = _rulesText
        };

        return JsonSerializer.Serialize(persistentData);
    }

    public void SetPersistentData(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            _rulesText = string.Empty;
            _rules.Clear();
            return;
        }

        try
        {
            var persistentData = JsonSerializer.Deserialize<PersistenceData>(data);
            if (persistentData != null)
            {
                var rulesText = persistentData.RulesText ?? string.Empty;
                ParseAndStoreRules(rulesText);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);

            // If deserialization fails, reset to empty
            _rulesText = string.Empty;
            _rules.Clear();
        }
    }

    private static string GetSampleRules()
    {
        return """
               // Sample rules
               // Lines starting with // are comments

               // Business layer should not access Data layer directly
               DENY: MyApp.Business.** -> MyApp.Data.**

               // Controllers may only access Services
               RESTRICT: MyApp.Controllers.** -> MyApp.Services.**

               // Core components may not depend on UI
               DENY: MyApp.Core.** -> MyApp.UI.**

               // Domain should be completely isolated
               ISOLATE: MyApp.Domain.**

               // Specific class restrictions
               DENY: MyApp.Models.User -> MyApp.Data.Database
               """;
    }

    private void ParseAndStoreRules(string rulesText)
    {
        _rules = RuleParser.ParseRules(rulesText);
        _rulesText = rulesText;
    }

    private List<Violation> ExecuteAnalysis(CodeGraph graph)
    {
        var violations = new List<Violation>();

        if (_rules.Count == 0)
            return violations;

        var allRelationships = graph.GetAllRelationships().ToList();

        // Group rules by type and source
        var denyRules = _rules.OfType<DenyRule>().ToList();
        var isolateRules = _rules.OfType<IsolateRule>().ToList();
        var restrictRules = _rules.OfType<RestrictRule>().ToList();

        // Process DENY rules (each is independent)
        foreach (var denyRule in denyRules)
        {
            var sourceIds = PatternMatcher.ResolvePattern(denyRule.Source, graph);
            var targetIds = PatternMatcher.ResolvePattern(denyRule.Target, graph);

            var ruleViolations = denyRule.ValidateRule(sourceIds, targetIds, allRelationships);
            if (ruleViolations.Count > 0)
            {
                violations.Add(new Violation(denyRule, ruleViolations));
            }
        }

        // Process ISOLATE rules (each is independent)
        foreach (var isolateRule in isolateRules)
        {
            var sourceIds = PatternMatcher.ResolvePattern(isolateRule.Source, graph);
            var emptyTargetIds = new HashSet<string>(); // Not used for ISOLATE

            var ruleViolations = isolateRule.ValidateRule(sourceIds, emptyTargetIds, allRelationships);
            if (ruleViolations.Count > 0)
            {
                violations.Add(new Violation(isolateRule, ruleViolations));
            }
        }

        // Process RESTRICT rules (group by source)
        var restrictGroups = restrictRules.GroupBy(r => r.Source).ToList();
        foreach (var group in restrictGroups)
        {
            var restrictGroup = new RestrictRuleGroup(group.Key, group);
            var sourceIds = PatternMatcher.ResolvePattern(group.Key, graph);

            // Collect all allowed target IDs from all rules in the group
            // References inside the source pattern are always allowed implicitly(!).
            var allowedTargetIds = new HashSet<string>(sourceIds);
            foreach (var restrictRule in group)
            {
                var targetIds = PatternMatcher.ResolvePattern(restrictRule.Target, graph);
                allowedTargetIds.UnionWith(targetIds);
            }

            restrictGroup.AllowedTargetIds = allowedTargetIds;

            var groupViolations = restrictGroup.ValidateGroup(sourceIds, allRelationships);
            if (groupViolations.Count > 0)
            {
                // Use first rule in group as representative for violation
                violations.Add(new Violation(group.First(), groupViolations));
            }
        }

        return violations;
    }
}