using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Assistant;
using RDCore.SDK.Client;
using NewtonsoftConverter = Newtonsoft.Json.JsonConverterAttribute;

namespace RDCore.SDK.Platform.Protocol;

// the rdcore/tools/* requests a client sends, as the platform's JSON-RPC handlers and senders know them. Their Protocol DTOs hold JSON (the schemas, the
// arguments, the structured results), which only System.Text.Json writes as the wire has it: every wrapper is written through it (SystemTextJsonBridge).

/// <summary>
/// <see cref="ToolsListParams"/> as a request of <c>rdcore/tools/list</c>, answered with a <see cref="ToolsListReply"/>: the type a language server's
/// handler of the request takes.
/// </summary>
[Method(RDCorePlatformProtocol.ToolsList, Direction.ClientToServer)]
[NewtonsoftConverter(typeof(SystemTextJsonBridge<ToolsListRequest>))]
public sealed record class ToolsListRequest : ToolsListParams, IRequest, IRequest<ToolsListReply>;

/// <summary>
/// <see cref="ToolsListResult"/> as the answer to <see cref="ToolsListRequest"/>.
/// </summary>
[NewtonsoftConverter(typeof(SystemTextJsonBridge<ToolsListReply>))]
public sealed record class ToolsListReply : ToolsListResult
{
    /// <summary>
    /// An answer that lists no tool.
    /// </summary>
    public ToolsListReply() { }

    /// <summary>
    /// An answer of the tools a catalogue lists.
    /// </summary>
    /// <param name="listed">The tools.</param>
    public ToolsListReply(ToolsListResult listed) : base(listed) { }
}

/// <summary>
/// <see cref="ToolCallParams"/> as a request of <c>rdcore/tools/call</c>, answered with a <see cref="ToolCallReply"/>: the type a language server's
/// handler of the request takes.
/// </summary>
[Method(RDCorePlatformProtocol.ToolsCall, Direction.ClientToServer)]
[NewtonsoftConverter(typeof(SystemTextJsonBridge<ToolCallRequest>))]
public sealed record class ToolCallRequest : ToolCallParams, IRequest, IRequest<ToolCallReply>
{
    /// <summary>
    /// A call of no tool.
    /// </summary>
    public ToolCallRequest() { }

    /// <summary>
    /// A call of a tool.
    /// </summary>
    /// <param name="call">The tool and its arguments.</param>
    public ToolCallRequest(ToolCallParams call) : base(call) { }
}

/// <summary>
/// <see cref="ToolCallResult"/> as the answer to <see cref="ToolCallRequest"/>.
/// </summary>
[NewtonsoftConverter(typeof(SystemTextJsonBridge<ToolCallReply>))]
public sealed record class ToolCallReply : ToolCallResult
{
    /// <summary>
    /// An answer with no content.
    /// </summary>
    public ToolCallReply() { }

    /// <summary>
    /// An answer of what a tool answered.
    /// </summary>
    /// <param name="result">What the tool answered.</param>
    public ToolCallReply(ToolCallResult result) : base(result) { }
}
