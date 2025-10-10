using System.IO;
using System.Windows;
using System.Windows.Controls;
using CodeParser.Parser;
using CSharpCodeAnalyst.Analyzers;
using CSharpCodeAnalyst.Areas.AdvancedSearchArea;
using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Areas.InfoArea;
using CSharpCodeAnalyst.Areas.TreeArea;
using CSharpCodeAnalyst.CommandLine;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Refactoring;
using CSharpCodeAnalyst.Shared.Messages;
using Microsoft.Extensions.Configuration;

namespace CSharpCodeAnalyst;

public partial class App
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check if command line arguments are provided
        if (e.Args.Length > 0)
        {
            // Run in command-line mode.
            // ConsoleHelper.EnsureConsole();
            var exitCode = await CommandLineProcessor.ProcessCommandLine(e.Args);
            Environment.Exit(exitCode);
            return;
        }

        // Run in UI mode
        StartUi();

        if (e.Args.Length == 1)
        {
            // Allow loading a project file (json) via command line for faster debugging
            var dc = MainWindow?.DataContext as MainViewModel;
            if (dc != null)
            {
                dc.OnLo;
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
        var settings = configuration.GetSection("ApplicationSettings").Get<ApplicationSettings>();

        if (settings is null)
        {
            settings = new ApplicationSettings();
        }

        var messageBox = new WindowsMessageBox();
        var messaging = new MessageBus();

        var analyzerManager = new AnalyzerManager();
        analyzerManager.LoadAnalyzers(messaging, messageBox);

        var explorer = new CodeGraphExplorer();
        var mainWindow = new MainWindow();

        var explorationGraphViewer = new GraphViewer(messaging, settings.WarningCodeElementLimit);

        var refactoringInteraction = new RefactoringInteraction();
        var refactoringService = new RefactoringService(refactoringInteraction);

        mainWindow.SetViewer(explorationGraphViewer);
        var viewModel = new MainViewModel(messaging, settings, analyzerManager);
        var graphViewModel = new GraphViewModel(explorationGraphViewer, explorer, messaging, settings);
        var treeViewModel = new TreeViewModel(messaging, refactoringService);
        var searchViewModel = new SearchViewModel(messaging);
        var infoPanelViewModel = new InfoPanelViewModel();

        viewModel.InfoPanelViewModel = infoPanelViewModel;
        viewModel.GraphViewModel = graphViewModel;
        viewModel.TreeViewModel = treeViewModel;
        viewModel.SearchViewModel = searchViewModel;

        // Setup messaging
        messaging.Subscribe<LocateInTreeRequest>(mainWindow.HandleLocateInTreeRequest);
        messaging.Subscribe<ShowTabularDataRequest>(viewModel.HandleShowTabularData);
        messaging.Subscribe<AddNodeToGraphRequest>(graphViewModel.HandleAddNodeToGraphRequest);
        messaging.Subscribe<QuickInfoUpdateRequest>(infoPanelViewModel.HandleUpdateQuickInfo);
        messaging.Subscribe<CycleCalculationComplete>(viewModel.HandleCycleCalculationComplete);
        messaging.Subscribe<ShowPartitionsRequest>(viewModel.HandleShowPartitionsRequest);
  
        messaging.Subscribe<ShowCycleGroupRequest>(viewModel.HandleShowCycleGroupRequest);

        // Refactorings are forwarded to all other view models
        messaging.Subscribe<CodeGraphRefactored>(viewModel.HandleCodeGraphRefactored);
        

        mainWindow.DataContext = viewModel;
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}