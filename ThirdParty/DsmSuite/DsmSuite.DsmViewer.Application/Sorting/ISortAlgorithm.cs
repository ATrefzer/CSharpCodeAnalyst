// SPDX-License-Identifier: GPL-3.0-or-later
namespace DsmSuite.DsmViewer.Application.Sorting
{
    public interface ISortAlgorithm
    {
        SortResult Sort();
        string Name { get; }
    }
}
