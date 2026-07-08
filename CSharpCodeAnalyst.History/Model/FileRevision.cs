namespace CSharpCodeAnalyst.History.Model
{
    [Serializable]
    public sealed class FileRevision
    {
        public FileRevision(string localFile, string changeSetId, DateTime date, string cachePath)
        {
            ChangeSetId = changeSetId;
            Date = date;
            CachePath = cachePath;
            LocalFile = localFile;
        }

        /// <summary>
        ///     Cache path, where the file is downloaded
        /// </summary>
        public string CachePath { get; set; }

        public string ChangeSetId { get; }
        public DateTime Date { get; }
        public string LocalFile { get; }
    }
}