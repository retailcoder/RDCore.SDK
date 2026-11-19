using RDCore.SDK.Assistant;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Client;

/// <summary>
/// The tools of the language server a client app is connected to, for an assistant bridge (<see cref="McpToolServer"/>): <c>rdcore/tools/list</c> and
/// <c>rdcore/tools/call</c> sent over the app's LSP connection.
/// </summary>
/// <remarks>
/// The app must expect the <c>AssistantTools</c> capability (<see cref="CorePlatformClientCapabilities"/>): the language server refuses the requests of a
/// client that did not advertise it. Cancelling a request sends <c>$/cancelRequest</c>.
/// </remarks>
/// <param name="app">The client app whose connection the requests are sent over.</param>
public sealed class ClientAppToolSource(IRDCoreClientApp app) : IAssistantToolSource
{
    /// <summary>
    /// Completes once the app's connection to the language server is ready (<see cref="IRDCoreClientApp.WaitForReadyAsync"/>).
    /// </summary>
    /// <param name="cancellationToken">A token that stops the wait.</param>
    /// <returns>A task that completes when the connection is ready, and faults if it is lost first.</returns>
    public Task WaitForReadyAsync(CancellationToken cancellationToken) => app.WaitForReadyAsync(cancellationToken);

    /// <summary>
    /// Lists the tools (<c>rdcore/tools/list</c>).
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>Every tool of the language server's catalog.</returns>
    public async Task<ToolsListResult> ListAsync(CancellationToken cancellationToken)
        => await app.SendRequestAsync<ToolsListRequest, ToolsListReply>(new ToolsListRequest(), cancellationToken);

    /// <summary>
    /// Calls a tool (<c>rdcore/tools/call</c>).
    /// </summary>
    /// <param name="call">The tool and its arguments.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns>What the tool answers.</returns>
    public async Task<ToolCallResult> CallAsync(ToolCallParams call, CancellationToken cancellationToken)
        => await app.SendRequestAsync<ToolCallRequest, ToolCallReply>(new ToolCallRequest(call), cancellationToken);
}
