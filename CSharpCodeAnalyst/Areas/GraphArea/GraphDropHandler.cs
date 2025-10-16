using System.Windows;
using CSharpCodeAnalyst.Areas.TreeArea;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Shared.Contracts;
using GongSolutions.Wpf.DragDrop;

namespace CSharpCodeAnalyst.Areas.GraphArea;

/// <summary>
///     Handles drag and drop operations from the TreeView to the Graph area.
/// </summary>
internal sealed class GraphDropHandler : IDropTarget
{
    private readonly IPublisher _publisher;

    public GraphDropHandler(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public void DragOver(IDropInfo dropInfo)
    {
        // Check if the dragged data is a TreeItemViewModel
        if (dropInfo.Data is TreeItemViewModel treeItem && treeItem.CodeElement != null)
        {
            dropInfo.Effects = DragDropEffects.Copy;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
        }
        else
        {
            dropInfo.Effects = DragDropEffects.None;
        }
    }

    public void Drop(IDropInfo dropInfo)
    {
        // Extract the TreeItemViewModel from the drag data
        if (dropInfo.Data is TreeItemViewModel treeItem && treeItem.CodeElement != null)
        {
            // Publish the same message that the context menu uses
            _publisher.Publish(new AddNodeToGraphRequest(treeItem.CodeElement));
        }
    }
}
