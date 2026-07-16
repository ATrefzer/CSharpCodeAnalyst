// SPDX-License-Identifier: GPL-3.0-or-later
namespace DsmSuite.DsmViewer.Model.Interfaces
{
    public interface ISortResult
    {
        int GetIndex(int currentIndex);
        int GetNumberOfElements();
        bool IsValid { get; }
    }
}
