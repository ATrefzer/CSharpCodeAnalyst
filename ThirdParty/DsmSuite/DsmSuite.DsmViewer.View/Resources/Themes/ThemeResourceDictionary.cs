// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.ViewModel.Settings;
using System.Windows;

namespace DsmSuite.DsmViewer.View.Resources.Themes
{
    public class ThemeResourceDictionary : ResourceDictionary
    {
        /// <summary>
        /// Changed 2026-07 for CSharpCodeAnalyst: was App.Skin, a static on the DsmViewer App class.
        /// That class is gone now that the viewer is hosted as a control inside another application,
        /// so the host sets the theme here before the resource dictionaries are loaded.
        /// </summary>
        public static Theme Skin { get; set; } = Theme.Light;

        private Uri _lightThemeSource;
        private Uri _pastelThemeSource;
        private Uri _darkThemeSource;

        public Uri LightThemeSource
        {
            get { return _lightThemeSource; }
            set
            {
                _lightThemeSource = value;
                UpdateSource();
            }
        }

        public Uri PastelThemeSource
        {
            get { return _pastelThemeSource; }
            set
            {
                _pastelThemeSource = value;
                UpdateSource();
            }
        }


        public Uri DarkThemeSource
        {
            get { return _darkThemeSource; }
            set
            {
                _darkThemeSource = value;
                UpdateSource();
            }
        }

        private void UpdateSource()
        {
            Uri uri;
            switch (Skin)
            {
                case Theme.Pastel:
                    uri = PastelThemeSource;
                    break;
                case Theme.Light:
                    uri = LightThemeSource;
                    break;
                case Theme.Dark:
                    uri = DarkThemeSource;
                    break;
                default:
                    uri = LightThemeSource;
                    break;
            }

            if ((uri != null) && (Source != uri))
            {
                Source = uri;
            }
        }
    }
}
