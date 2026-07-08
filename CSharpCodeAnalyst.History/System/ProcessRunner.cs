using System.Diagnostics;
using System.Text;

namespace CSharpCodeAnalyst.History.System
{
    public sealed class ProcessResult
    {
        public int ExitCode { get; set; }
        public string StdErr { get; set; }
        public string StdOut { get; set; }
    }

    public class ProcessRunner
    {
        public Encoding DefaultEncoding { get; set; }

        public ProcessResult RunProcess(string pathToExecutable, string arguments)
        {
            return RunProcess(pathToExecutable, arguments, null);
        }

        /// <summary>
        /// ExitCode, StdOut, StdErr
        /// </summary>
        public ProcessResult RunProcess(string pathToExecutable, string arguments, string workingDirectory)
        {
            using (var process = CreateProcess(pathToExecutable, DefaultEncoding, workingDirectory))
            {
                if (!string.IsNullOrEmpty(arguments))
                {
                    process.StartInfo.Arguments = arguments;
                }

                process.Start();

                var stdOut = process.StandardOutput.ReadToEnd();
                var stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    StdOut = stdOut,
                    StdErr = stdErr
                };
            }
        }

        private static Process CreateProcess(string pathToExecutable, Encoding encoding, string workingDirectory = null)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = pathToExecutable,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = encoding,
                StandardErrorEncoding = encoding
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            var process = new Process
            {
                StartInfo = startInfo
            };

            return process;
        }
    }
}