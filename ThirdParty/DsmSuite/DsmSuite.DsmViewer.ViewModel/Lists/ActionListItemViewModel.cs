// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;

namespace DsmSuite.DsmViewer.ViewModel.Lists
{
    public class ActionListItemViewModel : ViewModelBase
    {
        private IAction _action;

        public ActionListItemViewModel(int index, IAction action)
        {
            Index = index;
            _action = action;
        }

        public int Index { get; }
        public IAction Action => _action;
        public string Title => _action.Title;
        public string Details => _action.Description;
    }
}
