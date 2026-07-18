// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows.Input;

namespace DsmSuite.DsmViewer.ViewModel.Matrix
{
    public interface IMatrixViewModel
    {
        ICommand ToggleElementExpandedCommand { get; }
        ICommand SortElementCommand { get; }
        ICommand MoveUpElementCommand { get; }
        ICommand MoveDownElementCommand { get; }

        ICommand ToggleElementBookmarkCommand { get; }

        ICommand ChangeElementParentCommand { get; }
    }
}
