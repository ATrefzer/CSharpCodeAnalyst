// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace DsmSuite.DsmViewer.ViewModel.Matrix
{
    /// <summary>
    /// Contains identification for a row/provider or column/consumer in the matrix.
    /// Note that Index may be null, even if Element is not (e.g. a non-leaf element).
    /// </summary>
    public class MatrixViewModelCoordinate
    {
        public enum AxisType {Row, Column};

        /// <summary>
        /// Indicates whether this is a row (provider) or column (consumer) coordinate.
        /// </summary>
        public AxisType Axis { get; init; }

        /// <summary>
        /// The element corresponding to this coordinate.
        /// Null iff this coordinate is empty.
        /// </summary>
        public IDsmElement Element { get; init; }

        /// <summary>
        /// The row/column number (0 based) of this coordinate, or null if empty,
        /// or there is no row/column (e.g. because it is expanded).
        /// </summary>
        public int? Index { get; init; }
    }
}
