using System.Reflection;
using DsmSuite.DsmViewer.Application.Core;
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
}