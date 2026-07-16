// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;

namespace DsmSuite.Common.Util
{
    public static class StringExtensions
    {
        public static string ReplaceIgnoreCase(this string input, string oldValue, string newValue)
        {
            return Regex.Replace(input, Regex.Escape(oldValue), newValue.Replace("$", "$$"), RegexOptions.IgnoreCase);
        }
    }
}
