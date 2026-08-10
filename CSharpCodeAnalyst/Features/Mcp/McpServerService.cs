using System.Diagnostics;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Mcp;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.Mcp;

/// <summary>
///     Owns the MCP server for the application: starting and stopping it on demand, reporting the
///     result, and handing out the line a user needs to register it with a client.
///     <para>
///         Nothing listens until someone asks for it. That is the point of driving this from a button
///         rather than a setting - a shipped application that opens a socket before anyone wanted one
///         is a decision nobody made.
///     </para>
/// </summary>
public sealed class McpServerService(
    AppSettings settings,
    CodeGraphSnapshotProvider snapshotProvider,
    IUserNotification notification)
{
    /// <summary>
    ///     The name the server is registered under in the client. Only a default - the user can pick
    ///     another - but it decides how the tools are addressed there (mcp__csca__graph_info), so it is
    ///     worth being short and recognizable.
    /// </summary>
    private const string DefaultClientName = "csca";

    private readonly McpServerHost _host = new();

    public bool IsRunning => _host.IsRunning;

    public Uri? Endpoint => _host.Endpoint;

    /// <summary>Raised after the server started or stopped, so the UI can follow.</summary>
    public event EventHandler? StateChanged;

    public async Task ToggleAsync()
    {
        if (IsRunning)
        {
            await StopAsync();
        }
        else
        {
            await StartAsync();
        }
    }

    /// <summary>
    ///     Starts the server. A failure here - most likely the port being taken - costs the MCP feature
    ///     and nothing else, so it is reported to the user rather than thrown at the application.
    /// </summary>
    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        try
        {
            await _host.StartAsync(snapshotProvider, settings.McpServerPort);
            notification.ShowSuccess(string.Format(Strings.Mcp_Started, _host.Endpoint));
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Starting the MCP server failed: {ex}");
            notification.ShowError(string.Format(Strings.Mcp_StartFailed, settings.McpServerPort,
                ex.Message));
        }
        finally
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            await _host.StopAsync();
        }
        catch (Exception ex)
        {
            // The socket is gone either way; a failure while shutting down must not leave the button
            // stuck in "running".
            Trace.TraceError($"Stopping the MCP server failed: {ex}");
        }
        finally
        {
            // Switching the server off gives back the second copy of the graph. Without this the
            // memory stays claimed for a feature nobody is using any more.
            snapshotProvider.Release();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Stops without waiting, for application shutdown. Deliberately not awaited by the caller:
    ///     a tool call still in flight captures the graph on the UI thread, so blocking that thread to
    ///     wait for the drain would block the drain itself. The process is going away with the socket.
    /// </summary>
    public void StopWithoutWaiting()
    {
        if (IsRunning)
        {
            _ = StopAsync();
        }
    }

    /// <summary>
    ///     The command that registers this server with Claude Code. Handed over ready to paste, because
    ///     every part of it is easy to get wrong: the default scope binds the entry to whatever
    ///     directory the user happened to be in, and the port has to match the running server.
    /// </summary>
    public string GetClientSetupCommand()
    {
        var endpoint = Endpoint?.ToString() ??
                       $"http://127.0.0.1:{settings.McpServerPort}{McpServerHost.EndpointPath}";
        return $"claude mcp add --scope user --transport http {DefaultClientName} {endpoint}";
    }

    public void CopyClientSetupCommand()
    {
        try
        {
            Clipboard.SetText(GetClientSetupCommand());
            notification.ShowSuccess(Strings.Mcp_SetupCopied);
        }
        catch (Exception ex)
        {
            // Another process can hold the clipboard open. Not worth an error dialog - show the
            // command instead, so the user can still copy it by hand.
            Trace.TraceError($"Copying the MCP setup command failed: {ex}");
            notification.ShowInfo(GetClientSetupCommand());
        }
    }
}
