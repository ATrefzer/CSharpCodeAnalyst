using System.Windows.Controls;

namespace CSharpCodeAnalyst.GraphArea;

public interface IDependencyGraphBinding
{
    /// <summary>
    ///     Binds the given Panel to the viewer.
    ///     The graph is drawn on the panel.
    ///     Use DockPanel, Canvas does not work.
    /// </summary>
    void Bind(Panel graphPanel);
}