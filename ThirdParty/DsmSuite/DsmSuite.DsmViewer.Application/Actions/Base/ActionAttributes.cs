// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Text;

namespace DsmSuite.DsmViewer.Application.Actions.Base
{
    public class ActionAttributes
    {
        readonly Dictionary<string, string> _data;

        public ActionAttributes()
        {
            _data = new Dictionary<string, string>();
        }

        public void SetString(string memberName, string memberValue)
        {
            _data[RemoveUnderscore(memberName)] = memberValue;
        }

        public void SetInt(string memberName, int memberValue)
        {
            _data[RemoveUnderscore(memberName)] = memberValue.ToString();
        }

        public void SetNullableInt(string memberName, int? memberValue)
        {
            if (memberValue.HasValue)
            {
                _data[RemoveUnderscore(memberName)] = memberValue.Value.ToString();
            }
        }

        public void SetListInt(string memberName, List<int> list)
        {
            StringBuilder s = new StringBuilder();

            for (int i = 0; i < list.Count; i++)
            {
                s.Append(list[i]);
                if (i < list.Count - 1)
                    s.Append(',');
            }

            _data[RemoveUnderscore(memberName)] = s.ToString();
        }

        /// <summary>
        /// Set data for <c>memberName</c> to a compact representation of <c>list</c>.
        /// If <c>list</c> is not strictly increasing or contains negative numbers, the results are undefined.
        /// </summary>
        public void SetListIntCompact(string memberName, List<int> list)
        {
            StringBuilder s = new StringBuilder();
            int startRun, i;

            i = 0;
            while (i < list.Count)
            {
                if (list[i] < 0)  // This causes parsing problems for ActionReadOnlyAttributes
                    throw new ArgumentException("negative element in list");
                //if (i > 0  &&  list[i] <= list[i - 1])
                    //throw new ArgumentException("list must be strictly increasing");

                if (s.Length > 0)
                    s.Append(',');
                s.Append(list[i]);
                startRun = i;
                while (i < list.Count-1  &&  list[i+1] == list[i] + 1)
                {
                    i++;
                }
                if (i > startRun+1)
                {
                    s.Append('-');
                    s.Append(list[i]);
                    i++;
                }
                else if (i == startRun)
                    i++;
            }

            _data[RemoveUnderscore(memberName)] = s.ToString();
        }

        public IReadOnlyDictionary<string, string> Data => _data;

        private static string RemoveUnderscore(string memberName)
        {
            return memberName.Substring(1);
        }
    }
}
