using CodeParser.Extensions;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration
{
    /// <summary>
    /// Finds disjunct partitions of code elements that are related to each other.
    /// This helps to split large classes with low cohesion.
    /// </summary>
    internal class CodeElementPartitioner
    {
        public List<HashSet<string>> GetPartitions(CodeGraph codeGraph, CodeElement parentElement)
        {
            var subGraph = codeGraph.SubGraphOf(parentElement);

            // Remove the single parent element to avoid that everything is in one partition
            subGraph.RemoveCodeElement(parentElement.Id);

            var codeElementIdToPartition = InitializePartitions(subGraph);

            var allRelationships = subGraph.GetAllRelationships().ToList();
            foreach (var relationship in allRelationships)
            {
                var sourcePartition = codeElementIdToPartition[relationship.SourceId];
                var targetPartition = codeElementIdToPartition[relationship.TargetId];

                if (ReferenceEquals(sourcePartition, targetPartition))
                {
                    // Already in the same partition
                    continue;
                }

                MergePartitions(codeElementIdToPartition, sourcePartition, targetPartition);
            }


            return GetDistinctPartitions(codeElementIdToPartition);
        }

        private static void MergePartitions(Dictionary<string, HashSet<string>> codeElementIdToPartition, HashSet<string> sourcePartition, HashSet<string> targetPartition)
        {
            sourcePartition.UnionWith(targetPartition);
            foreach (var codeElementId in targetPartition)
            {
                codeElementIdToPartition[codeElementId] = sourcePartition;
            }
        }

        private static List<HashSet<string>> GetDistinctPartitions(Dictionary<string, HashSet<string>> codeElementIdToPartition)
        {
            return codeElementIdToPartition.Values.Distinct().ToList();
        }

        /// <summary>
        ///     Returns mapping from code element id to its partition (set of code element ids).
        ///     Basically we start with each code element in its own partition.
        ///     However, if we have a parent-child relationship, we consider all children of a code element
        ///     to be in the same partition.
        /// </summary>
        private static Dictionary<string, HashSet<string>> InitializePartitions(CodeGraph subGraph)
        {
            // We start with each code element in its own partition
            var codeElementIdToPartition = new Dictionary<string, HashSet<string>>((int)subGraph.VertexCount);
            foreach (var codeElement in subGraph.Nodes.Values)
            {
                codeElementIdToPartition[codeElement.Id] = new HashSet<string>();
            }

            // For the moment consider any sub-container as a single element.
            // So we already can initialize the partitions with all children of a code element
            foreach (var codeElement in subGraph.Nodes.Values)
            {
                var partition = codeElementIdToPartition[codeElement.Id];
                var children = codeElement.GetChildrenIncludingSelf();
                foreach (var child in children)
                {
                    // Merge partitions.
                    partition.Add(child);
                    codeElementIdToPartition[child] = partition;
                }
            }

            return codeElementIdToPartition;
        }
    }
}