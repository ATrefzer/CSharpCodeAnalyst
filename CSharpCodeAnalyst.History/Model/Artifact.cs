namespace CSharpCodeAnalyst.History.Model
{
    /// <summary>
    /// An artifact is a file committed to the source control system.
    /// </summary>
    public sealed class Artifact
    {
        public int Commits { get; set; }

        public HashSet<string> Committers { get; } = new HashSet<string>();

        /// <summary>
        /// If the source control system does not provide unique ids like in svn use the StringId with
        /// server path.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///  It still may be around on hard disk but removed from TFS!
        /// </summary>
        public bool IsDeleted { get; set; }

        public DateTime Date { get; set; }

        public string LocalPath { get; set; }

        public string Revision { get; set; }

        public string ServerPath { get; set; }
    }
}