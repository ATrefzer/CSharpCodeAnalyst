// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.Analyzer.Model.Interface;
using System.Collections.Generic;

namespace DsmSuite.Analyzer.Model.Persistency
{
    public interface IDsiElementModelFileCallback
    {
        IDsiElement ImportElement(int id, string name, string type, IDictionary<string, string> properties);
        IEnumerable<IDsiElement> GetElements();
        int CurrentElementCount { get; }
    }
}
