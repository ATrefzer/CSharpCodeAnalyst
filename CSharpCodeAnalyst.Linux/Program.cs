using CSharpCodeAnalyst.Linux;

using var cancellation = new CancellationTokenSource();

// Ctrl+C cancels the run instead of killing the process outright, so that ProcessRunner can stop
// doxygen and the temporary working directory is cleaned up.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

return await CommandLine.RunAsync(args, Console.Out, Console.Error, cancellation.Token);
