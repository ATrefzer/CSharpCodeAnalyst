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
    /// </summary>
    /// <remarks>
    ///     Safe to call off the UI thread, and meant to be: the work is quadratic in the number of types
    ///     (MatrixViewModel's constructor fills a weight and a colour for every cell), which is why the
    ///     caller runs it in the background with a progress indicator. Nothing in here touches a
    ///     DispatcherObject — DsmSuite's view models raise PropertyChanged into the void until something
    ///     binds to them, and their commands are CommunityToolkit RelayCommands, which have no dispatcher
    ///     affinity. Hand the result to the UI thread and bind it there.
    ///     <para>
    ///         The order matters: DsmApplication binds its query layer to the model instance it is
    ///         constructed with and never rebinds, so the model has to be fully populated first.
    ///     </para>
    /// </remarks>
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
