namespace RDCore.SDK.Assistant;

/// <summary>
/// Where an assistant bridge gets the tools it serves: the language server's catalog, through whatever connection the bridge has to it
/// (<c>rdcore/tools/list</c> and <c>rdcore/tools/call</c>).
/// </summary>
/// <remarks>
/// A <see cref="McpToolServer"/> serves the tools of one source. A client app fills it from its LSP connection (<c>ClientAppToolSource</c>); an IDE add-in
/// can fill it from its host's channel to the language server.
/// </remarks>
public interface IAssistantToolSource
{
    /// <summary>
    /// Completes once the language server can list and call its tools.
    /// </summary>
    /// <param name="cancellationToken">A token that stops the wait.</param>
    /// <returns>A task that completes when the tools can be listed and called.</returns>
    Task WaitForReadyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists the tools (<c>rdcore/tools/list</c>).
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>Every tool of the catalog.</returns>
    Task<ToolsListResult> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Calls a tool (<c>rdcore/tools/call</c>).
    /// </summary>
    /// <param name="call">The tool and its arguments.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns>What the tool answers; a refusal is a result whose <see cref="ToolCallResult.IsError"/> is <see langword="true"/>.</returns>
    /// <exception cref="Exception">The language server refused the request, or the connection failed; the message says why.</exception>
    Task<ToolCallResult> CallAsync(ToolCallParams call, CancellationToken cancellationToken);
}

/// <summary>
/// The bounds a <see cref="McpToolServer"/> keeps.
/// </summary>
public sealed record class McpLimits
{
    /// <summary>
    /// The bounds a server keeps unless it is given others: a wait of 60 seconds for the platform, and messages of at most 4 MiB.
    /// </summary>
    public static McpLimits Default { get; } = new();

    /// <summary>
    /// How long a <c>tools/list</c> or <c>tools/call</c> request waits for the platform to be ready (<see cref="IAssistantToolSource.WaitForReadyAsync"/>)
    /// before it fails with the JSON-RPC error <c>-32603</c>. <c>initialize</c> does not wait.
    /// </summary>
    public TimeSpan ReadyTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The longest message read, in bytes of UTF-8. A longer one is skipped to its end of line and answered with the JSON-RPC error <c>-32600</c>.
    /// </summary>
    public int MaxMessageLength { get; init; } = 4 * 1024 * 1024;
}

/// <summary>
/// The server, as a <see cref="McpToolServer"/> tells an assistant's host about it when it answers <c>initialize</c>.
/// </summary>
public sealed record class McpServerInfo
{
    /// <summary>
    /// The server's name (<c>serverInfo.name</c>).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// A name for a person to read (<c>serverInfo.title</c>); <see langword="null"/> for none.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The server's version (<c>serverInfo.version</c>).
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// What the assistant is told about using the server's tools (<c>instructions</c>); <see langword="null"/> for nothing.
    /// </summary>
    public string? Instructions { get; init; }
}
