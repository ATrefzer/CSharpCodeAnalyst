using System.Reflection;
using DsmSuite.Analyzer.Model.Core;
using DsmSuite.Common.Util;
using DsmSuite.DsmViewer.Application.Core;
using DsmSuite.DsmViewer.Application.Import.Dsi;
using DsmSuite.DsmViewer.Model.Core;
// Both this application and DsmSuite have a MainViewModel.
using DsmMainViewModel = DsmSuite.DsmViewer.ViewModel.Main.MainViewModel;

namespace CSharpCodeAnalyst.Features.DsmMatrix;

/// <summary>
///     Builds the view model behind the DSM tab from a code graph.
/// </summary>
public static class DsmMatrixFactory
{
    /// <summary>
    ///     Builds everything from scratch and hands back a view model ready to be shown.
    ///     Can be executed in a worker thread.
    ///     The order matters: DsmApplication binds its query layer to the model instance it is
    ///     constructed with and never rebinds, so the model has to be fully populated first.
    /// </summary>
    public static DsmMainViewModel Create(CodeGraph.Graph.CodeGraph codeGraph)
    {
        var model = new DsmModel("CSharpCodeAnalyst", Assembly.GetExecutingAssembly());
        var typeCount = new CodeGraphToDsmModelBuilder(model, codeGraph).Build();

        var application = new DsmApplication(model);
        var viewModel = new DsmMainViewModel(application);
        viewModel.ShowInMemoryModel($"{typeCount} types");

        return viewModel;
    }

    /// <summary>
    ///     Loads a DsmSuite model file into a view model ready to be shown - a <c>.dsm</c> (DsmSuite's native
    ///     model) or a <c>.dsi</c> (the analyzer intermediate, imported into a fresh model). Can be executed
    ///     in a worker thread.
    /// </summary>
    /// <remarks>
    ///     Same order as <see cref="Create" />: populate the model, then construct the
    ///     <see cref="DsmApplication" />, so its query layer binds to the finished model. DsmSuite's own file
    ///     path (AsyncOpenModel / AsyncImportDsiModel) swaps the model underneath an already constructed
    ///     application and never rebinds the queries - see ThirdParty/DsmSuite/README.md bug 9 - so it is
    ///     deliberately not used here.
    /// </remarks>
    public static DsmMainViewModel CreateFromFile(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var progress = new Progress<ProgressInfo>();
        var model = new DsmModel("Viewer", assembly);

        if (path.EndsWith(".dsi", StringComparison.OrdinalIgnoreCase))
        {
            // A .dsi carries the raw elements and relations; read it, then let the importer build the
            // hierarchical DSM model (auto-partitioned so the elements come out ordered).
            var dsiModel = new DsiModel("Viewer", new List<string>(), assembly);
            dsiModel.Load(path, progress);
            new DsiImporter(dsiModel, model, true).Import(progress);
        }
        else
        {
            model.LoadModel(path, progress);
        }

        var application = new DsmApplication(model);
        var viewModel = new DsmMainViewModel(application);
        viewModel.ShowInMemoryModel(System.IO.Path.GetFileName(path));

        return viewModel;
    }
}