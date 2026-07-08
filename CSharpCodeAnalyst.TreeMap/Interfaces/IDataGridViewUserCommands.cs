using System.Collections;
using System.Windows.Controls;

namespace CSharpCodeAnalyst.TreeMap.Interfaces
{
    public interface IDataGridViewUserCommands
    {
        bool Fill(ContextMenu contextMenu, IEnumerable selectedItems);
    }
}
