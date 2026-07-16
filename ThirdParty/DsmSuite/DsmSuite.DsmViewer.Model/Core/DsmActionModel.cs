// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.Model.Persistency;

namespace DsmSuite.DsmViewer.Model.Core
{
    /// <summary>
    /// Stores an ordered list of actions.<br/>
    /// actionIds are assigned in increasing order, if not provided by the caller.
    /// </summary>
    /// DsmModel forwards calls here.
    public class DsmActionModel : IDsmActionModelFileCallback
    {
        private readonly List<DsmAction> _actions;
        private int _lastActionId;

        public DsmActionModel()
        {
            _actions = new List<DsmAction>();
            _lastActionId = 0;
        }

        public void Clear()
        {
            _actions.Clear();
            _lastActionId = 0;
        }

        /// <summary>
        /// Create a new action with the given properties and append it to the list.
        /// </summary>
        /// <returns>The new action.</returns>
        public IDsmAction ImportAction(int id, string type, IReadOnlyDictionary<string, string> data,
                IEnumerable<IDsmAction> actions)
        {
            if (id > _lastActionId)
            {
                _lastActionId = id;
            }

            DsmAction action = new DsmAction(id, type, data, actions);
            _actions.Add(action);
            return action;
        }


        /// <summary>
        /// Create a new action with the given properties, assigning it a sequential number
        /// as id.
        /// </summary>
        /// <returns>The new action.</returns>
        public IDsmAction AddAction(string type, IReadOnlyDictionary<string, string> data,
                IEnumerable<IDsmAction> actions)
        {
            _lastActionId++;
            DsmAction action = new DsmAction(_lastActionId, type, data, actions);
            _actions.Add(action);
            return action;
        }


        public IEnumerable<IDsmAction> GetExportedActions()
        {
            return _actions;
        }

        public int GetExportedActionCount()
        {
            return _actions.Count;
        }
    }
}
