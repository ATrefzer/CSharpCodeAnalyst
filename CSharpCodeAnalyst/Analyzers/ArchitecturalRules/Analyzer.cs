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
    private readonly IUserNotification _userNotification;
    private readonly IPublisher _messaging;
    private List<RuleBase> _rules = [];
    private string _rulesText;
    private ArchitecturalRulesDialog? _openDialog;
    private CodeGraph? _currentGraph;
    private bool _isDirty;

    public Analyzer(IPublisher messaging, IUserNotification userNotification)
    {
        _messaging = messaging;
        _userNotification = userNotification;

        // Subscribe to application exit event to close dialog
        if (Application.Current != null)
        {
            Application.Current.Exit += OnApplicationExit;
        }

        _rulesText = GetSampleRules();
    }

    public void Analyze(CodeGraph graph)
    {
        // If dialog is already open, just bring it to front
        if (_openDialog != null)
        {
            _openDialog.Activate();
            return;
        }

        _currentGraph = graph;

        _openDialog = new ArchitecturalRulesDialog
        {
            // If we omit the owner, the dialog may appear behind the main window
            // However, it would be automatically closed when the main window closes.
            Owner = Application.Current.MainWindow,

            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            // Load existing rules or provide sample rules
            RulesText = string.IsNullOrEmpty(_rulesText) ? GetSampleRules() : _rulesText
        };

        // Set up validation callback
        _openDialog.OnValidateRequested = OnValidateRules;

        // Handle dialog closing
        _openDialog.Closed += (sender, args) =>
        {
            _openDialog = null;
            _currentGraph = null;
        };

        _openDialog.Show();
    }

    private void OnValidateRules(string rulesText)
    {
        if (_currentGraph == null)
        {
            return;
        }

        try
        {
            ParseAndStoreRules(rulesText);
        }
        catch (Exception ex)
        {
            _userNotification.ShowError($"Error parsing rules: {ex.Message}");
            return;
        }

        // Execute analysis
        var violations = ExecuteAnalysis(_currentGraph);

        if (violations.Count == 0)
        {
            _userNotification.ShowSuccess("No rule violations found!");
        }
        else
        {
            // Show violations in tabular format
            var violationsViewModel = new RuleViolationsViewModel(violations, _currentGraph);
            _messaging.Publish(new ShowTabularDataRequest(violationsViewModel));
        }
    }

    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        _openDialog?.Close();
    }

    public string Name
    {
        get => "Architectural rules";
    }

    public string Description { get; } = "Validates your architectural constraints based on user-defined rules";

    public string Id
    {
        get => "ArchitecturalRules";
    }

    void SetDirty(bool isDirty)
    {
        _isDirty = isDirty;
        if (_isDirty)
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? GetPersistentData()
    {
        SetDirty(false);
        
        if (string.IsNullOrEmpty(_rulesText))
        {
            return null;
        }

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
           SetDirty(false);
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

    public bool IsDirty()
    {
        return _isDirty;
    }

    public event EventHandler? DataChanged;

    /// <summary>
    ///     Direct analysis with rules from file (for command-line use)
    /// </summary>
    public List<Violation> Analyze(CodeGraph graph, string fileToRules)
    {
        ParseAndStoreRules(File.ReadAllText(fileToRules));
        return ExecuteAnalysis(graph);
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
        
        if (!_isDirty && _rulesText != rulesText)
        {
            SetDirty(true);
        }
         
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