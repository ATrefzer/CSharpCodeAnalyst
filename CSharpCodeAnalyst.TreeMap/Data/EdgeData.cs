namespace CSharpCodeAnalyst.TreeMap.Data
{
    public sealed class EdgeData
    {
        public readonly double Strength; // [0...1]

        /// <summary>
        /// If no display name is set for the node, the id is used.
        /// </summary>
        public EdgeData(string node1Id, string node2Id, double strength)
        {
            Node1Id = node1Id;
            Node1DisplayName = node1Id;
            Node2Id = node2Id;
            Node2DisplayName = node2Id;
            Strength = strength;
        }

        public string Node1DisplayName { get; set; }
        public string Node1Id { get; }
        public string Node2DisplayName { get; set; }
        public string Node2Id { get; }
    }
}