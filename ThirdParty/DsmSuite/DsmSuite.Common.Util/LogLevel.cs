// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace DsmSuite.Common.Util
{
    [Serializable]
    public enum LogLevel
    {
        None,
        User,
        Error,
        Warning,
        Info,
        Data,
        All
    }
}
