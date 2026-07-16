// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Model.Persistency
{
    /// <summary>
    /// Specifies the methods for adding actions to the model and reading them.
    /// DsmModelFile call methods in this to write actions obtained from XML, or to determine which actions
    /// to write to XML.
    /// </summary>
    public interface IDsmActionModelFileCallback
    {
        /// <summary>Create a new action with the given properties.</summary>
        /// <returns>The new action.</returns>
        IDsmAction ImportAction(int id, string type, IReadOnlyDictionary<string, string> data,
            IEnumerable<IDsmAction> actions);

        /// <summary>Return all actions in the model.</summary>
        IEnumerable<IDsmAction> GetExportedActions();

        /// <summary>Return the number of actions in the model.</summary>
        int GetExportedActionCount();
    }
}
