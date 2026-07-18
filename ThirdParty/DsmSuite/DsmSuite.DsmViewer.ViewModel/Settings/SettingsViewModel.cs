// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.Common.Util;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;
using System.Windows.Input;

namespace DsmSuite.DsmViewer.ViewModel.Settings
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IDsmApplication _application;
        private LogLevel _logLevel;
        private string _selectedThemeName;
        private string _help;

        private readonly Dictionary<Theme, string> _supportedThemes;

        public SettingsViewModel(IDsmApplication application)
        {
            _application = application;
            _supportedThemes = new Dictionary<Theme, string>
            {
                [Theme.Light] = "Light",
                [Theme.Dark] = "Dark",
                [Theme.Pastel] = "Pastel"
            };

            _logLevel = ViewerSetting.LogLevel;
            SelectedThemeName = _supportedThemes[ViewerSetting.Theme];
            Help = "";

            AcceptChangeCommand = RegisterCommand(AcceptChangeExecute);
        }

        public ICommand AcceptChangeCommand { get; }

        public string[] LogLevelNames => Enum.GetNames(typeof(LogLevel));

        public string LogLevel
        {
            get { return _logLevel.ToString(); }
            set
            {
                _logLevel = (LogLevel) Enum.Parse(typeof(LogLevel), value);
                RaisePropertyChanged();
            }
        }
        public string Help
        {
            get { return _help; }
            private set { _help = value; RaisePropertyChanged(); }
        }

        public List<string> SupportedThemeNames => _supportedThemes.Values.ToList();

        public string Version => SystemInfo.VersionLong;

        public string SettingsFilePath => $"Settings file {ViewerSetting.SettingsFilePath}";

        public string SelectedThemeName
        {
            get { return _selectedThemeName; }
            set
            {
                _selectedThemeName = value;
                if (_selectedThemeName != _supportedThemes[ViewerSetting.Theme])
                    Help = "Theme change requires an application restart";
                else
                    Help = "";
                RaisePropertyChanged();
            }
        }

        private void AcceptChangeExecute(object parameter)
        {
            // Immediately effective.
            Logger.LogLevel = _logLevel;

            // Save upon application exit.
            ViewerSetting.LogLevel = _logLevel;
            ViewerSetting.Theme = _supportedThemes.FirstOrDefault(x => x.Value == SelectedThemeName).Key;
        }
    }
}
