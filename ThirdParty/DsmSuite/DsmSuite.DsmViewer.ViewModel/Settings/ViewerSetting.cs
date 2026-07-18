// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.Common.Util;
using System.IO;

namespace DsmSuite.DsmViewer.ViewModel.Settings
{
    public static class ViewerSetting
    {
        private static readonly string ApplicationSettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DsmSuite");
        private static readonly string _settingsFilePath = Path.Combine(ApplicationSettingsFolder, "ViewerSettings.xml");

        private static ViewerSettingsData _viewerSettings = ViewerSettingsData.CreateDefault();

        public static void Read()
        {
            if (!Directory.Exists(ApplicationSettingsFolder))
            {
                Directory.CreateDirectory(ApplicationSettingsFolder);
            }

            FileInfo settingsFileInfo = new FileInfo(_settingsFilePath);
            if (!settingsFileInfo.Exists)
            {
                ViewerSettingsData.WriteToFile(_settingsFilePath, _viewerSettings);
            }
            else
            {
                _viewerSettings = ViewerSettingsData.ReadFromFile(settingsFileInfo.FullName);
            }
        }

        public static string SettingsFilePath => _settingsFilePath;

        public static LogLevel LogLevel
        {
            set { _viewerSettings.LogLevel = value; }
            get { return _viewerSettings.LogLevel; }
        }

        public static Theme Theme
        {
            set { _viewerSettings.Theme = value; }
            get { return _viewerSettings.Theme; }
        }

        public static void Write()
        {
            ViewerSettingsData.WriteToFile(_settingsFilePath, _viewerSettings);
        }
    }
}
