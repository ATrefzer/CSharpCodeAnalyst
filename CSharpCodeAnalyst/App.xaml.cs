using System.IO;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.CodeGraph.Exploration;
using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.CommandLine;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Features.AdvancedSearch;
using CSharpCodeAnalyst.Persistence.Json;
using CSharpCodeAnalyst.Features.Analyzers;
using CSharpCodeAnalyst.Features.Graph;
using CSharpCodeAnalyst.Features.Info;
using CSharpCodeAnalyst.Features.Refactoring;
using CSharpCodeAnalyst.Features.Tree;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Notifications;
using CSharpCodeAnalyst.Shared.Services;
using Microsoft.Extensions.Configuration;

namespace CSharpCodeAnalyst;

public partial class App
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check if command line arguments are provided
        if (e.Args.Length > 1)
        {
            // Run in command-line mode.
            // ConsoleHelper.EnsureConsole();
            var exitCode = await CommandLineProcessor.ProcessCommandLine(e.Args);
            Environment.Exit(exitCode);
            return;
        }

        // Run in UI mode
        StartUi();

        // Faster debugging
        await LoadProjectFileFromCommandLineAsync(e);
    }

    private async Task LoadProjectFileFromCommandLineAsync(StartupEventArgs e)
    {
        const string prefix = "-load:";
        if (e.Args.Length == 1 && e.Args[0].StartsWith(prefix))
        {
            var file = e.Args[0][prefix.Length..];
            if (!File.Exists(file))
            {
                return;
            }

            // Allow loading a project file (json) via command line for faster debugging
            if (MainWindow?.DataContext is MainViewModel dc)
            {
                await dc.LoadProjectFileAsync(file);
            }
        }
    }

    private void StartUi()
    {
        // const int delayMs = 200;
        // ToolTipService.InitialShowDelayProperty.OverrideMetadata(
        //     typeof(DependencyObject),
        //     new FrameworkPropertyMetadata(delayMs));
        // ToolTipService.BetweenShowDelayProperty.OverrideMetadata(
        //     typeof(DependencyObject),
        //     new FrameworkPropertyMetadata(delayMs));


        try
        {
            Initializer.InitializeMsBuildLocator();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }

        // Load application settings
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true);

        IConfiguration configuration = builder.Build();
        var applicationSettings = configuration.GetSection("ApplicationSettings").Get<AppSettings>();

        if (applicationSettings is null)
        {
            applicationSettings = new AppSettings();
        }

        var userSettings = UserPreferences.LoadOrCreate();

        var uiNotification = new WindowsUserNotification();
        var messaging = new MessageBus();

        // Shared store for optional per-member source metrics: filled by MainViewModel on import /
        // project load, read by the Method Complexity analyzer.
        var metricStore = new CodeGraph.Metrics.MetricStore();

        var analyzerManager = new AnalyzerManager();
        analyzerManager.LoadAnalyzers(messaging, uiNotification, metricStore);

        var explorer = new CodeGraphExplorer();
        var mainWindow = new MainWindow();

        // The shared, render-agnostic model the web view observes and drives.
        var graphViewState = new GraphViewState();

        // Graph search reads/writes the shared GraphViewState (search highlights live in
        // PresentationState, which the web view renders).
        var graphSearchViewModel = new GraphSearchViewModel(graphViewState);

        var refactoringInteraction = new RefactoringInteraction();
        var refactoringService = new RefactoringService(refactoringInteraction, messaging);
        mainWindow.SetViewer(graphViewState, messaging, messaging, applicationSettings);

        var projectStorage = new JsonProjectStorage();
        var projectService = new ProjectService(projectStorage, uiNotification, userSettings);

        var viewModel = new MainViewModel(messaging, applicationSettings, userSettings, analyzerManager, refactoringService, projectService, metricStore);
        var graphViewModel = new GraphViewModel(graphViewState, explorer, messaging, applicationSettings, refactoringService);
        var treeViewModel = new TreeViewModel(messaging, refactoringService);
        var searchViewModel = new AdvancedSearchViewModel(messaging, refactoringService);
        var infoPanelViewModel = new InfoPanelViewModel();

        viewModel.InfoPanelViewModel = infoPanelViewModel;
        viewModel.GraphViewModel = graphViewModel;
        viewModel.TreeViewModel = treeViewModel;
        viewModel.SearchViewModel = searchViewModel;
        viewModel.GraphSearchViewModel = graphSearchViewModel;

        // Setup messaging
        messaging.Subscribe<LocateInTreeRequest>(mainWindow.HandleLocateInTreeRequest);
        messaging.Subscribe<ShowTabularDataRequest>(viewModel.HandleShowTabularData);
        messaging.Subscribe<ShowHierarchicalDataRequest>(viewModel.HandleShowHierarchicalData);
        messaging.Subscribe<AddNodeToGraphRequest>(graphViewModel.HandleAddNodeToGraphRequest);
        messaging.Subscribe<ExploreSelectedRequest>(graphViewModel.HandleExploreSelectedRequest);
        messaging.Subscribe<RemoveSelectedElementsRequest>(graphViewModel.HandleRemoveSelectedRequest);
        messaging.Subscribe<QuickInfoUpdateRequest>(infoPanelViewModel.HandleUpdateQuickInfo);
        messaging.Subscribe<CycleCalculationComplete>(viewModel.HandleCycleCalculationComplete);
        messaging.Subscribe<ShowPartitionsRequest>(viewModel.HandleShowPartitionsRequest);
        messaging.Subscribe<ShowCycleGroupRequest>(viewModel.HandleShowCycleGroupRequest);
        messaging.Subscribe<OpenSourceLocationRequest>(r => SourceLocationNavigator.Open(r.Location));


        // Refactorings are forwarded to all other view models
        messaging.Subscribe<CodeGraphRefactored>(viewModel.HandleCodeGraphRefactored);
        // messaging.Subscribe<CodeElementsMoved>(viewModel.HandleCodeGraphRefactored);
        // messaging.Subscribe<CodeElementsDeleted>(viewModel.HandleCodeGraphRefactored);
        // messaging.Subscribe<CodeElementCreated>(viewModel.HandleCodeGraphRefactored);


        mainWindow.DataContext = viewModel;
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}