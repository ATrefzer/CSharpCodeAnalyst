using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;
    private readonly IUserNotification _userNotification;
    private CodeGraph.Graph.CodeGraph? _currentGraph;
    private bool _isDirty;
    private ArchitecturalRulesDialog? _openDialog;
    private List<RuleBase> _rules = [];
    private string _rulesText;

    // Violations of the last validation run - the source for "Accept Baseline".
    private List<Violation> _lastViolations = [];

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

    public void Analyze(CodeGraph.Graph.CodeGraph graph)
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
        _openDialog.OnAcceptBaselineRequested = OnAcceptBaseline;
        _openDialog.OnRemoveUnusedRulesRequested = OnRemoveUnusedRules;

        // Handle dialog closing
        _openDialog.Closed += (_, _) =>
        {
            _openDialog = null;
            _currentGraph = null;
            _lastViolations = [];
        };

        _openDialog.Show();
    }

    public string Name
    {
        get => Strings.Analyzer_ArchitecturalRules_Label;
    }

    public string Description { get; } = Strings.Analyzer_ArchitecturalRules_Tooltip;

    public string Id
    {
        get => "ArchitecturalRules";
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
                // Set directly instead of going through ParseAndStoreRules: loading a saved
                // project is not a user edit and must not mark the analyzer dirty.
                _rulesText = persistentData.RulesText ?? string.Empty;
                _rules = RuleParser.ParseRules(_rulesText);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);

            // If deserialization fails, reset to empty
            _rulesText = string.Empty;
            _rules.Clear();
        }

        SetDirty(false);
    }

    public bool IsDirty()
    {
        return _isDirty;
    }

    public event EventHandler? DataChanged;

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
            _userNotification.ShowError(string.Format(Strings.Analyzer_ArchitecturalRules_ParseError, ex.Message));
            return;
        }

        // Execute analysis
        var result = RuleEngine.Execute(_rules, _currentGraph);

        // Remember the violations so the user can freeze them as a baseline.
        _lastViolations = result.Violations;

        // The result table lives behind the modeless dialog, so every outcome also gets an inline
        // status line - otherwise validating looks like it did nothing when only violations changed.
        string status;
        if (result.Warnings.Count > 0)
        {
            _userNotification.ShowWarning(string.Join(Environment.NewLine, result.Warnings));
            status = string.Format(Strings.Rules_Status_Warnings, result.Warnings.Count, result.Violations.Count);
        }
        else if (result.Violations.Count > 0)
        {
            status = string.Format(Strings.Rules_Status_Violations, result.Violations.Count);
        }
        else
        {
            _userNotification.ShowSuccess(Strings.Analyzer_ArchitecturalRules_NoData);
            status = Strings.Rules_Status_Clean;
        }

        if (_openDialog != null)
        {
            _openDialog.HasViolations = result.Violations.Count > 0;
            _openDialog.StatusText = status;
        }

        // Show violations in tabular format
        var violationsViewModel = new RuleViolationsViewModel(result.Violations, _currentGraph, _messaging);
        _messaging.Publish(new ShowTabularDataRequest(Id, Name, violationsViewModel));
    }

    /// <summary>
    ///     Freezes the violations of the last validation as ALLOW exceptions and appends them to
    ///     the rules. Afterwards the rules are re-validated so the user sees the now-clean state.
    /// </summary>
    private void OnAcceptBaseline()
    {
        if (_currentGraph == null || _openDialog == null || _lastViolations.Count == 0)
        {
            return;
        }

        var baseline = BaselineGenerator.GenerateAllowRules(_lastViolations, _currentGraph, _openDialog.RulesText);
        if (string.IsNullOrEmpty(baseline))
        {
            return;
        }

        var existing = _openDialog.RulesText.TrimEnd();
        var header = string.Format(Strings.ArchitecturalRules_Baseline_Header, DateTime.Now);
        var newText = $"{existing}{Environment.NewLine}{Environment.NewLine}// {header}{Environment.NewLine}{baseline.TrimEnd()}{Environment.NewLine}";

        _openDialog.RulesText = newText;

        // Re-validate so the result table and the button state reflect the new baseline.
        OnValidateRules(newText);
    }

    /// <summary>
    ///     Removes rules that currently match no code element (behaviour-preserving) and re-validates.
    /// </summary>
    private void OnRemoveUnusedRules()
    {
        if (_currentGraph == null || _openDialog == null)
        {
            return;
        }

        var (cleaned, removed) = RuleCleaner.RemoveUnusedRules(_openDialog.RulesText, _currentGraph);
        if (removed == 0)
        {
            _userNotification.ShowSuccess(Strings.Analyzer_ArchitecturalRules_Cleanup_NothingToRemove);
            return;
        }

        _openDialog.RulesText = cleaned;

        // Re-validate so the result table and button states reflect the cleaned rule set.
        OnValidateRules(cleaned);

        _userNotification.ShowSuccess(string.Format(Strings.Analyzer_ArchitecturalRules_Cleanup_Removed, removed));
    }

    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        _openDialog?.Close();
    }

    private void SetDirty(bool isDirty)
    {
        _isDirty = isDirty;
        if (_isDirty)
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Direct analysis with rules from file (for command-line use)
    /// </summary>
    public RuleAnalysisResult Analyze(CodeGraph.Graph.CodeGraph graph, string fileToRules)
    {
        ParseAndStoreRules(File.ReadAllText(fileToRules));
        return RuleEngine.Execute(_rules, graph);
    }

    private static string GetSampleRules()
    {
        return """
               // Sample rules
               // Lines starting with // are comments

               // Business layer should not access Data layer directly
               DENY: MyApp.Business.** -> MyApp.Data.**

               // Exception: ALLOW never reports violations, it only suppresses
               // violations found by the other rules
               ALLOW: MyApp.Business.Reporting.** -> MyApp.Data.**

               // Controllers may only access Services
               RESTRICT: MyApp.Controllers.** -> MyApp.Services.**

               // Core components may not depend on UI
               DENY: MyApp.Core.** -> MyApp.UI.**

               // Completely isolated, define exceptions with ALLOW
               ISOLATE: MyApp.Meta.**

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
}