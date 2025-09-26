using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using System.Windows;
using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers.ConsistencyRules.Rules;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messaging;

namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules;

public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;
    private List<ConsistencyRuleBase> _rules = [];
    private string _rulesText = string.Empty;

    public Analyzer(IPublisher messaging)
    {
        _messaging = messaging;
    }

    public void Analyze(CodeGraph graph)
    {
        var dialog = new ConsistencyRulesDialog();
        dialog.Owner = Application.Current.MainWindow;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Load existing rules or provide sample rules
        dialog.RulesText = string.IsNullOrEmpty(_rulesText) ? GetSampleRules() : _rulesText;

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ParseAndStoreRules(dialog.RulesText);
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Error parsing rules: {ex.Message}", Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            // Execute analysis
            var rulesSummary = GetRulesSummary();
            MessageBox.Show($"Rules configured successfully!\n\n{rulesSummary}\n\nRules will be saved with the project.\n\nAnalysis implementation will follow in the next step.",
                "Consistency Rules", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public string Name { get; } = "Consistency Rules";
    public string Description { get; set; } = "Analyzes code consistency based on user-defined rules";

    public string Id { get; } = "ConsistencyRules";

    public string? GetPersistentData()
    {
        if (string.IsNullOrEmpty(_rulesText))
            return null;

        var persistentData = new ConsistencyRulesPersistentData
        {
            RulesText = _rulesText,
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
            var persistentData = JsonSerializer.Deserialize<ConsistencyRulesPersistentData>(data);
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

    private string GetSampleRules()
    {
        return """
               // Sample Consistency Rules
               // Lines starting with // are comments

               // Business layer should not access Data layer directly
               DENY: Business.** -> Data.**

               // Controllers may only access Services
               RESTRICT: Controllers.** -> Services.**

               // Core components may not depend on UI
               DENY: Core.** -> UI.**

               // Domain should be completely isolated
               ISOLATE: Domain.**
               """;
    }

    private void ParseAndStoreRules(string rulesText)
    {
        _rules = RuleParser.ParseRules(rulesText);
        _rulesText = rulesText;
    }

    private string GetRulesSummary()
    {
        if (_rules.Count == 0)
            return "No rules defined.";

        var summary = $"Found {_rules.Count} rules:\n\n";

        var denyRules = _rules.OfType<DenyRule>().ToList();
        var restrictRules = _rules.OfType<RestrictRule>().ToList();
        var isolateRules = _rules.OfType<IsolateRule>().ToList();

        if (denyRules.Any())
        {
            summary += $"DENY rules ({denyRules.Count}):\n";
            foreach (var rule in denyRules)
            {
                summary += $"  • {rule.Source} -> {rule.Target}\n";
            }
            summary += "\n";
        }

        if (restrictRules.Any())
        {
            summary += $"RESTRICT rules ({restrictRules.Count}):\n";
            foreach (var rule in restrictRules)
            {
                summary += $"  • {rule.Source} -> {rule.Target}\n";
            }
            summary += "\n";
        }

        if (isolateRules.Any())
        {
            summary += $"ISOLATE rules ({isolateRules.Count}):\n";
            foreach (var rule in isolateRules)
            {
                summary += $"  • {rule.Source}\n";
            }
        }

        return summary.TrimEnd();
    }

    
}