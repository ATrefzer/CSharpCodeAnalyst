namespace CSharpCodeAnalyst.History.Contracts
{
    /// <summary>
    /// This interface defines functions needed to analyze a source control system.
    /// </summary>
    public interface ISourceControlProvider
    {
        List<WarningMessage> Warnings { get; }

        /// <summary>
        /// Provides all information to query the source control system.
        /// </summary>
        /// <param name="projectBase">Root folder of the local repository.</param>
        /// <param name="cachePath">Directory where to cache files and logs.</param>
        /// <param name="workItemRegex">Optional regular expression to parse commit comments.</param>
        void Initialize(string projectBase, string cachePath, string workItemRegex);

        string BaseDirectory {get;}

        /// <summary>
        /// Developer name -> number of lines modified in the given file.
        /// </summary>
        Dictionary<string, uint> CalculateDeveloperWork(string localFile);

        /// <summary>
        /// Downloads all revisions of a single file into a file cache if not already present.
        /// Used to analyze metric trends of a file.
        /// </summary>
        List<FileRevision> ExportFileHistory(string localFile);

        /// <summary>
        /// Returns a hash set of all server paths currently tracked by the source control system.
        /// </summary>
        HashSet<string> GetAllTrackedFiles();

        /// <summary>
        /// Read the history from the source control provider and store it offline in the file system.
        /// Work data is can be very slow to calculate, especially for svn. Therefore it is optional.
        /// </summary>
        void UpdateCache(IProgress progress, bool includeWorkData, IFilter filtTypeFilter);

        /// <summary>
        /// Returns the cached history. You have to call UpdateCache first.
        /// </summary>
        ChangeSetHistory QueryChangeSetHistory();


        /// <summary>
        /// Work data is optional.
        /// Returns null if the work data is not cached.
        /// Local file path -> Contribution
        /// </summary>
        Dictionary<string, Contribution> QueryContribution();
    }
}