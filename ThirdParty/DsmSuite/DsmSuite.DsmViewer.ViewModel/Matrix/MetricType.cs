// SPDX-License-Identifier: GPL-3.0-or-later
namespace DsmSuite.DsmViewer.ViewModel.Matrix
{
    public enum MetricType
    {
            /// <summary>Given an element <c>x</c>,
            /// the number of elements in the tree rooted at <c>x</c>.</summary>
        NumberOfElements,
            /// <summary>Given an element <c>x</c>,
            /// the number of elements in the tree rooted at <c>x</c>, as a percentage (0-100) of the
            /// elements in the whole model.</summary>
        RelativeSizePercentage,
            /// <summary>Given an element <c>x</c>,
            /// the number of relations in the model such that the producer is a descendant of <c>x</c>
            /// (or <c>x</c> itself) and the consumer is not.</summary>
        IngoingRelations,
            /// <summary>Given an element <c>x</c>,
            /// the number of relations in the model such that the consumer is a descendant of <c>x</c>
            /// (or <c>x</c> itself) and the producer is not.</summary>
        OutgoingRelations,
            /// <summary>Given an element <c>x</c>,
            /// the number of relations in the model such that the consumer and producer are
            /// both descendants of <c>x</c> (or <c>x</c> itself).</summary>
        InternalRelations,
            /// <summary>Given an element <c>x</c>,
            /// the number of relations in the model such that the one end is a descendant of
            /// <c>x</c> (or <c>x</c> itself) and the other end is not.</summary>
        ExternalRelations,
            /// <summary>Given an element <c>x</c>,
            /// the number of hierarchical cycles between descendants of <c>x</c>.<br/>
            /// There is a hierarchical cycle between elements <c>a</c> and <c>b</c>, when the model
            /// contains a relation such that a descendant of <c>a</c> consumes a descendant of
            /// <c>b</c> and a relation such that a descendant of <c>b</c> consumes a descendant of <c>a</c>
            /// </summary>
        HierarchicalCycles,
            /// <summary>Given an element <c>x</c>,
            /// the number of system cycles between descendants of <c>x</c>.<br/>
            /// A system cycle is a relation between elements <c>a</c> and <c>b</c>, such that <c>a</c>
            /// is a consumer of <c>b</c> <i>and</i> <c>b</c> is a consumer of <c>a</c>.
            /// </summary>
        SystemCycles,
            /// <summary>Given an element <c>x</c>,
            /// the sum of system cycles (see <see cref="SystemCycles"/>) and hierarchical cycles
            /// (see <see cref="HierarchicalCycles"/>) for <c>x</c>.</summary> for <c>x</c>"/>
        Cycles,
            /// <summary>Given an element <c>x</c>,
            /// the number of cycles <c>x</c> (see <see cref="Cycles"/>), as a percentage (0-100) of the
            /// number of internal relations of <c>x</c> (see <see cref="InternalRelations"/>).
            /// </summary>
        CyclicityPercentage,
    }
}
