using System.Windows.Controls;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public interface IGraphBinding
{
    /// <summary>
    ///     Binds the given Panel to the viewer.
    ///     The graph is drawn on the panel.
    ///     Use DockPanel, Canvas does not work.
    /// </summary>
    void Bind(Panel graphPanel);
}