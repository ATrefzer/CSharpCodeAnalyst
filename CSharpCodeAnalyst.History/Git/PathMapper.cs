namespace CSharpCodeAnalyst.History.Git
{
    public sealed class PathMapper
    {
        private readonly string _startDirectory;

        public PathMapper(string startDirectory)
        {
            _startDirectory = startDirectory;
        }

        public string MapToLocalFile(string serverPath)
        {
            var decoded = Decoder.DecodeEscapedBytes(serverPath);

            // In git we have the restriction 
            // that we cannot choose any subdirectory.
            // (Current knowledge). Select the one with .git for the moment.

            // Example
            // _startDirectory = d:\\....\Insight
            // serverPath = Insight/Board.txt
            // localPath = d:\\....\Insight\Insight/Board.txt
            var serverNormalized = decoded.Replace("/", "\\");
            var localPath = Path.Combine(_startDirectory, serverNormalized);
            return localPath;
        }
    }
}