// SPDX-License-Identifier: GPL-3.0-or-later
namespace DsmSuite.DsmViewer.Model.Interfaces
{
    public interface IDsmAction
    {
        int Id { get; }

        string Type { get; }

        IReadOnlyDictionary<string, string> Data { get; }

        /// <summary>
        /// Sub-actions or null.
        /// </summary>
        IEnumerable<IDsmAction> Actions { get; }
    }
}
