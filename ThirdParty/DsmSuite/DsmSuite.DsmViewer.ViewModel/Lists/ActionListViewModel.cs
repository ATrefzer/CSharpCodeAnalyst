// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;
using System.Text;
using System.Windows.Input;
using System.Windows;

namespace DsmSuite.DsmViewer.ViewModel.Lists
{
    public class ActionListViewModel : ViewModelBase
    {
        private readonly IDsmApplication _application;
        private IEnumerable<ActionListItemViewModel> _actions;

        public ActionListViewModel(IDsmApplication application)
        {
            Title = "Edit history";

            _application = application;
            _application.ActionPerformed += OnActionPerformed;

            UpdateActionList();

            CopyToClipboardCommand = RegisterCommand(CopyToClipboardExecute);
            ClearCommand = RegisterCommand(ClearExecute);
            GotoCommand = RegisterCommand(GotoExecute, GotoCanExecute);
        }

        private void OnActionPerformed(object sender, System.EventArgs e)
        {
            UpdateActionList();
        }

        public string Title { get; }
        public string SubTitle { get; }

        public IEnumerable<ActionListItemViewModel> Actions
        {
            get { return _actions; }
            set { _actions = value; RaisePropertyChanged(); }
        }

        public ICommand CopyToClipboardCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand GotoCommand { get; }

        private void CopyToClipboardExecute(object parameter)
        {
            StringBuilder builder = new StringBuilder();
            foreach (ActionListItemViewModel viewModel in Actions)
            {
                builder.AppendLine($"{viewModel.Index, -5}, {viewModel.Title, -30}, {viewModel.Details}");
            }
            Clipboard.SetText(builder.ToString());
        }

        private void GotoExecute(object parameter)
        {
            _application.GotoAction(((ActionListItemViewModel)parameter).Action);
        }

        private bool GotoCanExecute(object parameter)
        {
            return parameter != null;
        }

        private void ClearExecute(object parameter)
        {
            _application.ClearActions();
            UpdateActionList();
        }

        private void UpdateActionList()
        {
            var actionViewModels = new List<ActionListItemViewModel>();
            int index = 1;
            foreach (IAction action in _application.GetActions())
            {
                actionViewModels.Add(new ActionListItemViewModel(index, action));
                index++;
            }

            Actions = actionViewModels;
        }
    }
}
