﻿using System.IO;
using System.Windows;
using CodeParser.Parser;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.CycleArea;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.GraphArea;
using CSharpCodeAnalyst.TreeArea;
using Microsoft.Build.Tasks;
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


        var messaging = new MessageBus();
        var explorer = new CodeGraphExplorer();
        var mainWindow = new MainWindow();

        var explorationGraphViewer = new GraphViewer(messaging);

        mainWindow.SetViewer(explorationGraphViewer);
        var viewModel = new MainViewModel(messaging, settings);
        var graphViewModel = new GraphViewModel(explorationGraphViewer, explorer, messaging, settings);
        var treeViewModel = new TreeViewModel(messaging);
        var cycleViewModel = new CycleSummaryViewModel();
        viewModel.GraphViewModel = graphViewModel;
        viewModel.TreeViewModel = treeViewModel;
        viewModel.CycleSummaryViewModel = cycleViewModel;


        // Setup messaging

        // Find in tree triggered in graph context menu, handled in the main window.
        messaging.Subscribe<LocateInTreeRequest>(mainWindow.HandleLocateInTreeRequest);

        // Adding a node triggered in tree view, handled in graph view
        messaging.Subscribe<AddNodeToGraphRequest>(graphViewModel.HandleAddNodeToGraphRequest);

        // Context-sensitive help triggered in the graph, handled in the main view model
        messaging.Subscribe<QuickInfoUpdate>(viewModel.HandleUpdateQuickInfo);

        messaging.Subscribe<CycleCalculationComplete>(cycleViewModel.HandleCycleCalculationComplete);

        messaging.Subscribe<DeleteFromModelRequest>(viewModel.HandleDeleteFromModel);

        mainWindow.DataContext = viewModel;
        mainWindow.Show();
    }
}