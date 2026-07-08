namespace CSharpCodeAnalyst.History.Model
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
        void Initialize(string projectBas);

        string BaseDirectory {get;}

        /// <summary>
        /// Developer name -> number of lines modified in the given file.
        /// </summary>
        Dictionary<string, uint> CalculateDeveloperWork(string localFile);

        /// <summary>
        /// Downloads all revisions of a single file into a file cache if not already present.
        /// Used to analyze metric trends of a file.
        /// </summary>
        List<FileRevision> ExportFileHistory(DirectoryInfo cache, string localFile);

        /// <summary>
        /// Returns a hash set of all server paths currently tracked by the source control system.
        /// </summary>
        HashSet<string> GetAllTrackedFiles();

        Git.History ExtractHistory(IProgress progress, bool includeWorkData, IFilter fileTypeFilter);
    }
}