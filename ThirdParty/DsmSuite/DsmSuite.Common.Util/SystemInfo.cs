// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Reflection;

namespace DsmSuite.Common.Util
{
    public class SystemInfo
    {
        public static string GetExecutableInfo(Assembly assembly)
        {
            string name = assembly.GetName().Name;
            string version = assembly.GetName().Version.ToString();
            DateTime buildDate = new FileInfo(assembly.Location).LastWriteTime;
            return $"{name} version={version} build={buildDate}";
        }

        private static string Changes => ThisAssembly.Git.Commits != "0" ? $"-{ThisAssembly.Git.Commits}" : "";
        public static string Version => $"{ThisAssembly.Git.BaseTag}{Changes} {ThisAssembly.Git.Commit}";
        public static string VersionLong => $"DsmSuite version {Version}";
    }
}
