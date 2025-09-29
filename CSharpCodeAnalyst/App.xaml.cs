using System.IO;
using System.Windows;
using CodeParser.Parser;
using CSharpCodeAnalyst.Analyzers;
using CSharpCodeAnalyst.CommandLine;
using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Areas.InfoArea;
using CSharpCodeAnalyst.Areas.SearchArea;
using CSharpCodeAnalyst.Areas.TreeArea;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Shared.Messages;
using Microsoft.Extensions.Configuration;

namespace CSharpCodeAnalyst;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
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
        StartUI();
    }

    private void StartUI()
    {
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

        mainWindow.SetViewer(explorationGraphViewer);
        var viewModel = new MainViewModel(messaging, settings, analyzerManager);
        var graphViewModel = new GraphViewModel(explorationGraphViewer, explorer, messaging, settings);
        var treeViewModel = new TreeViewModel(messaging);
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
        messaging.Subscribe<QuickInfoUpdate>(infoPanelViewModel.HandleUpdateQuickInfo);
        messaging.Subscribe<CycleCalculationComplete>(viewModel.HandleCycleCalculationComplete);
        messaging.Subscribe<ShowPartitionsRequest>(viewModel.HandleShowPartitionsRequest);
        messaging.Subscribe<DeleteFromModelRequest>(viewModel.HandleDeleteFromModel);
        messaging.Subscribe<ShowCycleGroupRequest>(viewModel.HandleShowCycleGroupRequest);

        mainWindow.DataContext = viewModel;
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}