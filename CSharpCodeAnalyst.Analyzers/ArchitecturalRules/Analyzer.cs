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

        // Handle dialog closing
        _openDialog.Closed += (_, _) =>
        {
            _openDialog = null;
            _currentGraph = null;
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

        if (result.Warnings.Count > 0)
        {
            _userNotification.ShowWarning(string.Join(Environment.NewLine, result.Warnings));
        }

        if (result.Violations.Count == 0)
        {
            // Don't claim success when rules silently matched nothing.
            if (result.Warnings.Count == 0)
            {
                _userNotification.ShowSuccess(Strings.Analyzer_ArchitecturalRules_NoData);
            }
        }

        // Show violations in tabular format
        var violationsViewModel = new RuleViolationsViewModel(result.Violations, _currentGraph, _messaging);
        _messaging.Publish(new ShowTabularDataRequest(Id, Name, violationsViewModel));
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