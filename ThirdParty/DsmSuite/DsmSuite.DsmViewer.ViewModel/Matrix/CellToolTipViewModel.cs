// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;

namespace DsmSuite.DsmViewer.ViewModel.Matrix
{
    public class CellToolTipViewModel : ViewModelBase
    {
        /// <summary>
        /// Changed 2026-07 for CSharpCodeAnalyst: took a weight and a CycleType as well, and exposed them as
        /// Weight and CycleType. The cell already draws the weight as its number and a cycle as its colour,
        /// so the tooltip only restated what the pointer is sitting on. Dropping them also spares
        /// UpdateCellTooltip a GetDependencyWeight and an IsCyclicDependency on every hover.
        /// The Legend property went with them: its single entry described the cycle colour.
        /// </summary>
        public CellToolTipViewModel(IDsmElement consumer, IDsmElement provider)
        {
            Title = $"Relation {consumer.Name} - {provider.Name}";
            ConsumerId = consumer.Id;
            ConsumerName = consumer.Fullname;
            ProviderId = provider.Id;
            ProviderName = provider.Fullname;
        }

        public string Title { get; }
        public int ConsumerId { get; }
        public string ConsumerName { get; }
        public int ProviderId { get; }
        public string ProviderName { get; }
    }
}
