// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Model.Core
{
    public class DsmAction : IDsmAction
    {
        private List<IDsmAction> _actions;

        /// <summary>
        /// Create a new action. For every sub-action, an equivalent DsmAction is created recursively.
        /// </summary>
        public DsmAction(int id, string type, IReadOnlyDictionary<string, string> data,
                IEnumerable<IDsmAction> actions = null)
        {
            Id = id;
            Type = type;
            Data = data;
            _actions = actions == null ? null : new List<IDsmAction>(actions.Select(
                    a => a is DsmAction ? a : new DsmAction(a.Id, a.Type, a.Data, a.Actions)));
        }

        public int Id { get; }

        public string Type { get; }

        public IReadOnlyDictionary<string, string> Data { get; }

        public IEnumerable<IDsmAction> Actions { get { return _actions; } }
    }
}
