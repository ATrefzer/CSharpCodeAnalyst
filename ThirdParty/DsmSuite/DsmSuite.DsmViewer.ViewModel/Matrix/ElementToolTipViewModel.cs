// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;

namespace DsmSuite.DsmViewer.ViewModel.Matrix
{
    public class ElementToolTipViewModel : ViewModelBase
    {
        /// <summary>
        /// Changed 2026-07 for CSharpCodeAnalyst: dropped Id, DsmSuite's own element numbering. It
        /// identifies nothing outside the DSM model - the code graph keys on its own ids - so it was a
        /// number the reader could do nothing with. Same reasoning as in CellToolTipViewModel.
        /// </summary>
        public ElementToolTipViewModel(IDsmElement element, IDsmApplication application)
        {
            Title = $"Element {element.Name}";
            Name = element.Fullname;
            Type = element.Type;

            Legend = new List<LegendViewModel>();
            Legend.Add(new LegendViewModel(LegendColor.Consumer, "Consumer"));
            Legend.Add(new LegendViewModel(LegendColor.Provider, "Provider"));
            Legend.Add(new LegendViewModel(LegendColor.Cycle, "Cycle"));
            Legend.Add(new LegendViewModel(LegendColor.Search, "Search"));
            Legend.Add(new LegendViewModel(LegendColor.Bookmark, "Bookmark"));
        }

        public string Title { get; }
        public string Name { get; }
        public string Type { get; }
        public List<LegendViewModel> Legend { get; }
    }
}
