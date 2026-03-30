using System.Windows;
using CSharpCodeAnalyst.Areas.AdvancedSearchArea;
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
        if (dropInfo.Data is TreeItemViewModel { CodeElement: not null })
        {
            dropInfo.Effects = DragDropEffects.Copy;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
        }
        else if (dropInfo.Data is SearchItemViewModel { CodeElement: not null })
        {
            dropInfo.Effects = DragDropEffects.Copy;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
        }
        else if (dropInfo.Data is List<object> list && list.OfType<SearchItemViewModel>().Any())
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
        if (dropInfo.Data is TreeItemViewModel { CodeElement: not null } treeItem)
        {
            // Publish the same message that the context menu uses
            _publisher.Publish(new AddNodeToGraphRequest(treeItem.CodeElement));
        }
        else if (dropInfo.Data is SearchItemViewModel { CodeElement: not null } searchItem)
        {
            _publisher.Publish(new AddNodeToGraphRequest(searchItem.CodeElement));
        }
        else if (dropInfo.Data is List<object> list)
        {
            var elements = list
                .OfType<SearchItemViewModel>()
                .Where(s => s.CodeElement != null)
                .Select(s => s.CodeElement)
                .ToList();
            
            if (elements.Any())
            {
                _publisher.Publish(new AddNodeToGraphRequest(elements!, false));
            }
        }
    }
}
