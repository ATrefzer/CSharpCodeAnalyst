// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Interfaces
{
    public record WeightedElement(IDsmElement Element, int weight);
}
