// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;

namespace DsmSuite.DsmViewer.ViewModel.Matrix
{
    public class CellToolTipViewModel : ViewModelBase
    {
        /// <summary>
        /// Changed 2026-07 for CSharpCodeAnalyst: dropped CycleType and the Legend that described its
        /// colour, and dropped the consumer and provider element ids.
        /// </summary>
        /// <remarks>
        /// The ids are DsmSuite's own element numbering. They identify nothing outside the DSM model - the
        /// code graph keys on its own ids and the user never sees either - so the tooltip spent two of its
        /// six lines on a number with no use.
        /// <para>
        /// The weight was dropped with the cycle type and is back: the argument was that the cell already
        /// draws it, which stops being true zoomed out. Below roughly a third of full size the number no
        /// longer fits the cell, so a populated cell and an empty one look alike and the tooltip is the
        /// only way left to tell them apart - which is exactly the zoom level where you are hunting for
        /// where the dependencies sit. The cycle type stays out: it is drawn as the cell colour, and a
        /// colour survives any zoom.
        /// </para>
        /// </remarks>
        public CellToolTipViewModel(IDsmElement consumer, IDsmElement provider, int weight)
        {
            Title = $"Relation {consumer.Name} \u2192 {provider.Name}";
            ConsumerName = consumer.Fullname;
            ProviderName = provider.Fullname;
            Weight = weight;
        }

        public string Title { get; }
        public string ConsumerName { get; }
        public string ProviderName { get; }
        public int Weight { get; }
    }
}
