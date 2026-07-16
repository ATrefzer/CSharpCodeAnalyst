// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Actions.Management
{
    /// <summary>
    /// A recursive representation of an IMultiAction, used by the ActionStore for saving to the DsmModel.
    /// Id is always 0.
    /// </summary>
    public class MultiActionDTO : IDsmAction
    {
        public int Id { get; }

        public string Type { get; }

        public IReadOnlyDictionary<string, string> Data { get; }

        public IEnumerable<IDsmAction> Actions { get; }

        public MultiActionDTO(IAction action)
        {
            Id = 0;
            Type = action.Type.ToString();  // Must be kept in sync with the code in ActionStore
            Data = action.Data;
            Actions = (action as IMultiAction)?.Actions.Select(a => new MultiActionDTO(a));
        }
    }
}
