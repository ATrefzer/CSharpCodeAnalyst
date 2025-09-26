using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using System.Windows;
using Contracts.Graph;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messaging;

namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules;

public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;
    private List<ConsistencyRule> _rules = [];
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
               DENY: Business.** !-> Data.**

               // Controllers may only access Services
               ISOLATE: Controllers.** -> Services.**

               // Core components may not depend on UI
               DENY: Core.** !-> UI.**

               // Allow specific exceptions
               ALLOW: Core.Logging.** -> UI.Controls.MessageBox
               """;
    }

    private void ParseAndStoreRules(string rulesText)
    {
        if (string.IsNullOrWhiteSpace(rulesText))
            return;

        var newRules = new List<ConsistencyRule>();
        var lines = rulesText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip comments and empty lines
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//"))
                continue;

            var rule = new ConsistencyRule
            {
                RuleText = trimmedLine,
                Description = $"Parsed rule: {trimmedLine}",
                IsEnabled = true
            };

            newRules.Add(rule);
        }

        _rules.Clear();
        _rules.AddRange(newRules);
        _rulesText = rulesText;
    }

    
}