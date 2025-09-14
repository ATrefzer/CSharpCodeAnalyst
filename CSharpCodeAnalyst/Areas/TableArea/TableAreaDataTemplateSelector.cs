using System.Windows;
using System.Windows.Controls;

namespace CSharpCodeAnalyst.Areas.ResultArea;

public class TableAreaDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CycleGroupCollectionTemplate { get; set; }
    public DataTemplate? PartitionCollectionTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is CycleGroupsViewModel)
        {
            return CycleGroupCollectionTemplate;
        }

        if (item is PartitionsViewModel)
        {
            return PartitionCollectionTemplate;
        }

        return base.SelectTemplate(item, container);
    }
}