using RDCore.SDK.Client;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RDCore.SDK.Assistant;

/// <summary>
/// Serves the tools of an <see cref="IAssistantToolSource"/> to an assistant's host, as a <strong>Model Context Protocol</strong> (2025-06-18) server: the
/// bridge between an assistant and the language server's <c>rdcore/tools/*</c> requests.
/// </summary>
/// <remarks>
/// <para>
/// Messages are JSON-RPC 2.0, one per line of UTF-8 (the MCP stdio transport), over any pair of streams. The tools and their results go through as the source
/// answers them: <see cref="ToolDescriptor"/> and <see cref="ToolCallResult"/> are already the MCP shapes.
/// </para>
/// <list type="table">
/// <listheader><term>Message</term><description>Answer</description></listheader>
/// <item><term><c>initialize</c></term><description>At once, without waiting for the platform: the protocol version <see cref="ProtocolVersion"/> (whichever
/// one the host asked for), the <c>tools</c> capability, <see cref="McpServerInfo"/>.</description></item>
/// <item><term><c>ping</c></term><description>An empty result.</description></item>
/// <item><term><c>tools/list</c></term><description>Every tool, in one answer; a <c>cursor</c> is ignored.</description></item>
/// <item><term><c>tools/call</c></term><description>The tool's result. A refusal of the tool itself is a result with <c>isError</c>.</description></item>
/// <item><term><c>notifications/cancelled</c></term><description>Cancels the request it names, which is then not answered.</description></item>
/// <item><term>Any other notification</term><description>Nothing.</description></item>
/// </list>
/// <para>
/// <c>tools/list</c> and <c>tools/call</c> wait for <see cref="IAssistantToolSource.WaitForReadyAsync"/> for at most <see cref="McpLimits.ReadyTimeout"/>.
/// They run as they come, several at a time; the answers are written one at a time, in the order they complete.
/// </para>
/// <para>
/// JSON-RPC errors: <c>-32700</c> for a message that is not JSON, <c>-32600</c> for one that is not a request or a notification (a batch, a message longer
/// than <see cref="McpLimits.MaxMessageLength"/>, the id of a request still running), <c>-32601</c> for an unknown method, <c>-32602</c> for a
/// <c>tools/call</c> that names no tool or whose parameters cannot be read, and <c>-32603</c> for a platform that is not ready in time and for any failure of
/// the source, with its message. Messages are in the language of <see cref="CultureInfo.CurrentUICulture"/>.
/// </para>
/// </remarks>
/// <param name="tools">Where the tools come from.</param>
/// <param name="server">The server, as <c>initialize</c> describes it.</param>
/// <param name="limits">The bounds the server keeps; <see cref="McpLimits.Default"/> when <see langword="null"/>.</param>
public sealed class McpToolServer(IAssistantToolSource tools, McpServerInfo server, McpLimits? limits = null)
{
    /// <summary>
    /// The version of the Model Context Protocol the server speaks.
    /// </summary>
    public const string ProtocolVersion = "2025-06-18";

    /// <summary>
    /// JSON-RPC 2.0: the message is not JSON.
    /// </summary>
    public const int ParseError = -32700;

    /// <summary>
    /// JSON-RPC 2.0: the message is not a request or a notification the server reads.
    /// </summary>
    public const int InvalidRequest = -32600;

    /// <summary>
    /// JSON-RPC 2.0: the server has no such method.
    /// </summary>
    public const int MethodNotFound = -32601;

    /// <summary>
    /// JSON-RPC 2.0: the parameters of the request cannot be read.
    /// </summary>
    public const int InvalidParams = -32602;

    /// <summary>
    /// JSON-RPC 2.0: the request failed in the server.
    /// </summary>
    public const int InternalError = -32603;

    private readonly McpLimits _limits = limits ?? McpLimits.Default;

    /// <summary>
    /// Reads and answers messages until <paramref name="input"/> ends or <paramref name="cancellationToken"/> is cancelled. The requests still running then
    /// are cancelled, and are not answered.
    /// </summary>
    /// <param name="input">Where the host's messages are read: the server's standard input, under the stdio transport.</param>
    /// <param name="output">Where the answers are written: the server's standard output, under the stdio transport. Nothing else may write to it.</param>
    /// <param name="cancellationToken">A token that stops the server.</param>
    /// <returns>A task that completes when the server has stopped.</returns>
    public async Task RunAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var writer = new MessageWriter(output);
        var reader = new LineReader(input, _limits.MaxMessageLength);
        var running = new ConcurrentDictionary<string, CancellationTokenSource>();
        var pending = new ConcurrentDictionary<Task, byte>();
        try
        {
            while (await reader.ReadAsync(stop.Token) is { } line)
            {
                if (line.TooLong)
                {
                    await writer.ErrorAsync(null, InvalidRequest, Format(McpMessages.MessageTooLong, _limits.MaxMessageLength));
                    continue;
                }

                JsonNode? node;
                try
                {
                    node = JsonNode.Parse(line.Text);
                }
                catch (JsonException)
                {
                    await writer.ErrorAsync(null, ParseError, McpMessages.NotJson);
                    continue;
                }

                if (node is not JsonObject message || !IsJsonRpc(message) || MethodOf(message) is not { } method)
                {
                    // an answer of the host: the server sends no request, so there is nothing to match it to.
                    if (node is JsonObject answer && answer.ContainsKey("id") && (answer.ContainsKey("result") || answer.ContainsKey("error")))
                    {
                        continue;
                    }

                    await writer.ErrorAsync(IdOf(node), InvalidRequest, McpMessages.NotARequest);
                    continue;
                }

                if (!message.TryGetPropertyValue("id", out var id))
                {
                    Notified(method, message["params"], running);
                    continue;
                }

                if (id is not JsonValue value || value.GetValueKind() is not (JsonValueKind.String or JsonValueKind.Number))
                {
                    await writer.ErrorAsync(null, InvalidRequest, McpMessages.NotARequest);
                    continue;
                }

                switch (method)
                {
                    case "initialize":
                        await writer.ResultAsync(id, Initialized());
                        break;
                    case "ping":
                        await writer.ResultAsync(id, new JsonObject());
                        break;
                    case "tools/list" or "tools/call":
                        var key = id.ToJsonString();
                        var call = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                        if (!running.TryAdd(key, call))
                        {
                            call.Dispose();
                            await writer.ErrorAsync(id, InvalidRequest, McpMessages.DuplicateId);
                            break;
                        }

                        // off the reading loop, so that the next message, a cancellation of this one included, is read while it runs.
                        var (requestId, parameters) = (id.DeepClone(), message["params"]?.DeepClone());
                        var served = Task.Run(() => ServedAsync(requestId, method, parameters, key, call), CancellationToken.None);
                        pending.TryAdd(served, 0);
                        _ = served.ContinueWith(task => pending.TryRemove(task, out _), TaskScheduler.Default);
                        break;
                    default:
                        await writer.ErrorAsync(id, MethodNotFound, Format(McpMessages.NoSuchMethod, method));
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
        finally
        {
            await stop.CancelAsync();
            await Task.WhenAll(pending.Keys);
        }

        async Task ServedAsync(JsonNode id, string method, JsonNode? parameters, string key, CancellationTokenSource call)
        {
            try
            {
                await ServeAsync(id, method, parameters, writer, call.Token);
            }
            finally
            {
                running.TryRemove(key, out _);
                call.Dispose();
            }
        }
    }

    private async Task ServeAsync(JsonNode id, string method, JsonNode? parameters, MessageWriter writer, CancellationToken token)
    {
        try
        {
            ToolCallParams? call = null;
            if (method == "tools/call")
            {
                try
                {
                    call = parameters?.Deserialize<ToolCallParams>(RDCorePlatformJson.Options);
                }
                catch (JsonException exception)
                {
                    await writer.ErrorAsync(id, InvalidParams, Format(McpMessages.ParametersNotRead, exception.Message));
                    return;
                }

                if (string.IsNullOrEmpty(call?.Name))
                {
                    await writer.ErrorAsync(id, InvalidParams, McpMessages.NoToolNamed);
                    return;
                }
            }

            try
            {
                await tools.WaitForReadyAsync(token).WaitAsync(_limits.ReadyTimeout, token);
            }
            catch (TimeoutException)
            {
                await writer.ErrorAsync(id, InternalError, Format(McpMessages.NotReady, _limits.ReadyTimeout.TotalSeconds));
                return;
            }

            var result = call is null
                ? JsonSerializer.SerializeToNode(await tools.ListAsync(token), RDCorePlatformJson.Options)
                : JsonSerializer.SerializeToNode(await tools.CallAsync(call, token), RDCorePlatformJson.Options);
            await writer.ResultAsync(id, result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // a cancelled request is not answered.
        }
        catch (Exception exception)
        {
            await writer.ErrorAsync(id, InternalError, exception.Message);
        }
    }

    private static void Notified(string method, JsonNode? parameters, ConcurrentDictionary<string, CancellationTokenSource> running)
    {
        if (method == "notifications/cancelled" && parameters?["requestId"] is JsonValue requestId && running.TryGetValue(requestId.ToJsonString(), out var call))
        {
            try
            {
                call.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // the request completed as it was cancelled.
            }
        }
    }

    private JsonObject Initialized()
    {
        var info = new JsonObject { ["name"] = server.Name };
        if (server.Title is { } title)
        {
            info["title"] = title;
        }

        info["version"] = server.Version;
        var result = new JsonObject
        {
            ["protocolVersion"] = ProtocolVersion,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = info,
        };
        if (server.Instructions is { } instructions)
        {
            result["instructions"] = instructions;
        }

        return result;
    }

    private static bool IsJsonRpc(JsonObject message)
        => message["jsonrpc"] is JsonValue version && version.TryGetValue<string>(out var text) && text == "2.0";

    private static string? MethodOf(JsonObject message)
        => message["method"] is JsonValue method && method.TryGetValue<string>(out var text) ? text : null;

    private static JsonNode? IdOf(JsonNode? node)
        => node is JsonObject message && message["id"] is JsonValue id && id.GetValueKind() is JsonValueKind.String or JsonValueKind.Number ? id.DeepClone() : null;

    private static string Format(string format, object argument) => string.Format(CultureInfo.CurrentCulture, format, argument);

    private sealed record class Line(string Text, bool TooLong);

    // one message a line: bytes up to a line feed, a carriage return before it dropped, blank lines skipped.
    private sealed class LineReader(Stream input, int maxLength)
    {
        private readonly byte[] _buffer = new byte[16 * 1024];
        private readonly MemoryStream _line = new();
        private int _start;
        private int _end;

        public async Task<Line?> ReadAsync(CancellationToken token)
        {
            _line.SetLength(0);
            var tooLong = false;
            while (true)
            {
                if (_start == _end)
                {
                    _start = 0;
                    _end = await input.ReadAsync(_buffer, token);
                    if (_end == 0)
                    {
                        return tooLong ? new Line(string.Empty, true) : IsBlank() ? null : new Line(Text(), false);
                    }
                }

                var newline = Array.IndexOf(_buffer, (byte)'\n', _start, _end - _start);
                var stop = newline < 0 ? _end : newline;
                if (!tooLong)
                {
                    if (_line.Length + (stop - _start) > maxLength)
                    {
                        tooLong = true;
                        _line.SetLength(0);
                    }
                    else
                    {
                        _line.Write(_buffer, _start, stop - _start);
                    }
                }

                _start = newline < 0 ? _end : newline + 1;
                if (newline >= 0)
                {
                    if (tooLong)
                    {
                        return new Line(string.Empty, true);
                    }

                    if (!IsBlank())
                    {
                        return new Line(Text(), false);
                    }

                    _line.SetLength(0);
                }
            }
        }

        private bool IsBlank()
        {
            var bytes = _line.GetBuffer();
            for (var index = 0; index < _line.Length; index++)
            {
                if (bytes[index] is not ((byte)' ' or (byte)'\t' or (byte)'\r'))
                {
                    return false;
                }
            }

            return true;
        }

        private string Text()
        {
            var length = (int)_line.Length;
            if (length > 0 && _line.GetBuffer()[length - 1] == (byte)'\r')
            {
                length--;
            }

            return System.Text.Encoding.UTF8.GetString(_line.GetBuffer(), 0, length);
        }
    }

    // the answers, one a line, one at a time. Once the host has closed its end, nothing more is written.
    private sealed class MessageWriter(Stream output)
    {
        private static readonly byte[] NewLine = [(byte)'\n'];
        private readonly SemaphoreSlim _gate = new(1, 1);
        private bool _closed;

        public Task ResultAsync(JsonNode id, JsonNode? result)
            => WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = result });

        public Task ErrorAsync(JsonNode? id, int code, string message)
            => WriteAsync(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
            });

        private async Task WriteAsync(JsonObject message)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, RDCorePlatformJson.Options);
            await _gate.WaitAsync();
            try
            {
                if (_closed)
                {
                    return;
                }

                await output.WriteAsync(bytes);
                await output.WriteAsync(NewLine);
                await output.FlushAsync();
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                _closed = true;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
