namespace CSharpCodeAnalyst.History.Config
{
    /// <summary>
    /// These values are overwritten from the application settings!
    /// </summary>
    public static class Thresholds
    {
        public static int MaxWorkItemsPerCommitForSummary { get; set; }= 200;

        public static int MinCommitsForHotspots { get; set; } = 3;
        public static int MinLinesOfCodeForHotspot { get; set; } = 1;

        /// <summary>
        /// Changesets with more modifications are ignored in change coupling.
        /// </summary>
        public static int MaxItemsInChangesetForChangeCoupling { get; set; } = 300;

        /// <summary>
        /// Reduces the output for change coupling
        /// </summary>
        public static int MinCouplingForChangeCoupling { get; set; } = 20;
        public static double MinDegreeForChangeCoupling { get; set; } = 40.0;
    }
}