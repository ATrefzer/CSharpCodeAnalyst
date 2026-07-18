// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.Common.Util;
using DsmSuite.DsmViewer.ViewModel.Common;

namespace DsmSuite.DsmViewer.ViewModel.Main
{
    public class ProgressViewModel : ViewModelBase
    {
        public event EventHandler<bool> BusyChanged;

        private bool _busy;
        private string _action;
        private string _text;
        private int _progressValue;
        private string _progressText;

        public void Update(ProgressInfo progress)
        {
            Text = progress.ActionText;
            if (progress.Percentage.HasValue)
            {
                ProgressText = $"{progress.CurrentItemCount}/{progress.TotalItemCount} {progress.ItemType}";
                ProgressValue = progress.Percentage.Value;
            }
            Busy = !progress.Done;
        }

        public string Action
        {
            get { return _action; }
            set { _action = value; RaisePropertyChanged(); }
        }

        public string Text
        {
            get { return _text; }
            set { _text = value; RaisePropertyChanged(); }
        }

        public bool Busy
        {
            get { return _busy; }
            set
            {
                if (_busy != value)
                {
                    _busy = value;
                    RaisePropertyChanged();
                    BusyChanged?.Invoke(this, _busy);
                }
            }
        }

        public int ProgressValue
        {
            get { return _progressValue; }
            set { _progressValue = value; RaisePropertyChanged(); }
        }

        public string ProgressText
        {
            get { return _progressText; }
            set { _progressText = value; RaisePropertyChanged(); }
        }
    }
}
