namespace CSharpCodeAnalyst.History.Model
{
    /// <summary>
    /// For example Tfs WorkItem or a Jira Task.
    /// In Tfs a commit is assigned to one or many WorkItems.
    /// Unfortunately in Svn alone this information is missing.
    /// It has to be queried from an external source.
    /// </summary>
    [Serializable]
    public sealed class WorkItem
    {
        public readonly string Id;
        public string Title;
        public string WorkItemTypeName;

        public WorkItem(string id)
        {
            Id = id;
        }

        /// <summary>
        ///     Used to identify teams. Tfs terminology.
        /// </summary>
        public string AreaPath { get; set; }

        public override bool Equals(object obj)
        {
            return obj is WorkItem item &&
                   EqualityComparer<string>.Default.Equals(Id, item.Id);
        }

        public override int GetHashCode()
        {
            return 2108858624 + EqualityComparer<string>.Default.GetHashCode(Id);
        }

        public bool IsBug()
        {
            return WorkItemTypeName.ToUpperInvariant() == "BUG";
        }
    }
}