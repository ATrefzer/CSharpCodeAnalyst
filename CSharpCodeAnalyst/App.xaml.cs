using System.IO;
using System.Windows;
using CodeParser.Parser;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.GraphArea;
using CSharpCodeAnalyst.InfoPanel;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.SearchArea;
using CSharpCodeAnalyst.TreeArea;
using Microsoft.Extensions.Configuration;

namespace CSharpCodeAnalyst;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        var messaging = new MessageBus();
        var explorer = new CodeGraphExplorer();
        var mainWindow = new MainWindow();

        var explorationGraphViewer = new GraphViewer(messaging, settings.WarningCodeElementLimit);

        mainWindow.SetViewer(explorationGraphViewer);
        var viewModel = new MainViewModel(messaging, settings);
        var graphViewModel = new GraphViewModel(explorationGraphViewer, explorer, messaging, settings);
        var treeViewModel = new TreeViewModel(messaging);
        var searchViewModel = new SearchViewModel(messaging);
        var infoPanelViewModel = new InfoPanelViewModel();

        viewModel.InfoPanelViewModel = infoPanelViewModel;
        viewModel.GraphViewModel = graphViewModel;
        viewModel.TreeViewModel = treeViewModel;
        viewModel.SearchViewModel = searchViewModel;


        // Setup messaging

        // Find in tree triggered in graph context menu, handled in the main window.
        messaging.Subscribe<LocateInTreeRequest>(mainWindow.HandleLocateInTreeRequest);

        messaging.Subscribe<ShowPluginResult>(viewModel.HandleShowPluginResult);

        // Adding a node triggered in tree view, handled in graph view
        messaging.Subscribe<AddNodeToGraphRequest>(graphViewModel.HandleAddNodeToGraphRequest);

        // Context-sensitive help triggered in the graph, handled in the info panel
        messaging.Subscribe<QuickInfoUpdate>(infoPanelViewModel.HandleUpdateQuickInfo);

        messaging.Subscribe<CycleCalculationComplete>(viewModel.HandleCycleCalculationComplete);

        messaging.Subscribe<ShowPartitionsRequest>(viewModel.HandleShowPartitionsRequest);
        messaging.Subscribe<DeleteFromModelRequest>(viewModel.HandleDeleteFromModel);

        messaging.Subscribe<ShowCycleGroupRequest>(viewModel.HandleShowCycleGroupRequest);

        mainWindow.DataContext = viewModel;
        mainWindow.Show();
    }
}