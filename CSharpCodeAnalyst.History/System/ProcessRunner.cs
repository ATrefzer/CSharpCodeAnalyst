using System.Diagnostics;
using System.Text;

namespace CSharpCodeAnalyst.History.System
{
    public sealed class ProcessResult
    {
        public required int ExitCode { get; init; }
        public required string StdErr { get; init; }
        public required string StdOut { get; init; }
    }

    public class ProcessRunner
    {
        public Encoding DefaultEncoding { get; init; } = Encoding.UTF8;

        public ProcessResult RunProcess(string pathToExecutable, string arguments)
        {
            return RunProcess(pathToExecutable, arguments, null);
        }

        /// <summary>
        /// ExitCode, StdOut, StdErr
        /// </summary>
        public ProcessResult RunProcess(string pathToExecutable, string arguments, string? workingDirectory)
        {
            var stdOut = string.Empty;
            var result = RunProcess(pathToExecutable, arguments, workingDirectory, reader => stdOut = reader.ReadToEnd());

            return new ProcessResult
            {
                ExitCode = result.ExitCode,
                StdOut = stdOut,
                StdErr = result.StdErr
            };
        }

        /// <summary>
        /// Lets the caller read the standard output while the process is running, so a large
        /// output does not have to fit into a single string. StdOut of the result is empty.
        /// </summary>
        public ProcessResult RunProcess(string pathToExecutable, string arguments, string? workingDirectory, Action<StreamReader> readStdOut)
        {
            using (var process = CreateProcess(pathToExecutable, DefaultEncoding, workingDirectory))
            {
                if (!string.IsNullOrEmpty(arguments))
                {
                    process.StartInfo.Arguments = arguments;
                }

                process.Start();

                // Both pipes have to be drained at the same time. A process that fills the
                // error pipe while we are still waiting on the output pipe blocks forever.
                var stdErr = process.StandardError.ReadToEndAsync();

                try
                {
                    readStdOut(process.StandardOutput);
                }
                catch
                {
                    // Nobody reads the output anymore. Do not leave the process blocked on a full pipe.
                    Kill(process);
                    throw;
                }

                process.WaitForExit();

                return new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    StdOut = string.Empty,
                    StdErr = stdErr.GetAwaiter().GetResult()
                };
            }
        }

        private static void Kill(Process process)
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }
        }

        private static Process CreateProcess(string pathToExecutable, Encoding encoding, string? workingDirectory = null)
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