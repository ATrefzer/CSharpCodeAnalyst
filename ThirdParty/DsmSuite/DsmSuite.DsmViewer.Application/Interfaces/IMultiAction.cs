// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DsmSuite.DsmViewer.Application.Interfaces
{
    /// <summary>
    /// An action that can contain a sequence of sub-actions.
    /// </summary>
    public interface IMultiAction : IAction
    {
        IEnumerable<IAction> Actions { get; }
    }
}
