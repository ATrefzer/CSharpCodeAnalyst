using System.Reflection;
using System.Windows.Controls;
using DsmSuite.DsmViewer.Application.Core;
using DsmSuite.DsmViewer.Model.Core;

// Both this application and DsmSuite have a MainViewModel.
using DsmMainViewModel = DsmSuite.DsmViewer.ViewModel.Main.MainViewModel;

namespace CSharpCodeAnalyst.Features.DsmMatrix;

/// <summary>
///     Hosts DsmSuite's matrix view as a tab. See <see cref="CodeGraphToDsmModelBuilder" /> for how the code
///     graph gets into the DSM model.
/// </summary>
public partial class DsmMatrixView : UserControl
{
    public DsmMatrixView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Rebuilds the matrix from the given code graph. Everything is constructed from scratch: DsmSuite's
    ///     application object binds its query layer to the model instance it is constructed with and does not
    ///     rebind when the model is swapped, so the model has to be populated before the application exists.
    /// </summary>
    public void Show(CodeGraph.Graph.CodeGraph codeGraph)
    {
        var model = new DsmModel("CSharpCodeAnalyst", Assembly.GetExecutingAssembly());
        var typeCount = new CodeGraphToDsmModelBuilder(model, codeGraph).Build();

        var application = new DsmApplication(model);
        var mainViewModel = new DsmMainViewModel(application);
        mainViewModel.ShowInMemoryModel($"{typeCount} types");

        DataContext = mainViewModel;
    }
}
