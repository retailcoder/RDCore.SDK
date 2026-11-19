using NSubstitute;
using RDCore.SDK.Assistant;
using RDCore.SDK.Client;
using RDCore.SDK.Platform.Protocol;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

namespace RDCore.Tests.Platform;

/// <summary>
/// An assistant's host speaks MCP to the bridge, one JSON-RPC message a line, and the bridge passes the language server's tools and results through as
/// they are: these sessions run a <see cref="McpToolServer"/> over a pair of pipes, as a host runs one over a process's standard input and output.
/// </summary>
[TestClass]
public sealed class McpToolServerTests
{
    private static readonly McpServerInfo Server = new() { Name = "rdcore", Title = "RDCore", Version = "0.1.5", Instructions = "Lines and columns are 1-based." };

    private static readonly ToolsListResult Catalog = new()
    {
        Tools =
        [
            new ToolDescriptor
            {
                Name = CoreAssistantTools.References,
                Description = "The names that refer to the symbol at a position.",
                InputSchema = new JsonObject { ["type"] = "object", ["required"] = new JsonArray("path") },
                Annotations = new ToolAnnotations { ReadOnlyHint = true },
            },
        ],
    };

    private const string Initialize = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"host","version":"1"}}}""";

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task Initialize_IsAnsweredAtOnce_WhileThePlatformIsNotReady()
    {
        var tools = new Tools { Ready = new TaskCompletionSource().Task };
        await using var session = Session.Start(tools);

        await session.SendAsync(Initialize);
        var answer = await session.NextAsync();

        Assert.AreEqual(1, (int)answer["id"]!);
        var result = answer["result"]!.AsObject();
        Assert.AreEqual(McpToolServer.ProtocolVersion, (string)result["protocolVersion"]!);
        Assert.IsFalse((bool)result["capabilities"]!["tools"]!["listChanged"]!);
        Assert.AreEqual("""{"name":"rdcore","title":"RDCore","version":"0.1.5"}""", result["serverInfo"]!.ToJsonString());
        Assert.AreEqual(Server.Instructions, (string)result["instructions"]!);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ToolsList_PassesTheCatalogThroughInItsShape()
    {
        await using var session = Session.Start(new Tools());

        await session.SendAsync("""{"jsonrpc":"2.0","id":"a","method":"tools/list","params":{}}""");
        var answer = await session.NextAsync();

        Assert.AreEqual("a", (string)answer["id"]!);
        Assert.AreEqual(
            """{"tools":[{"name":"references","description":"The names that refer to the symbol at a position.","inputSchema":{"type":"object","required":["path"]},"annotations":{"readOnlyHint":true,"destructiveHint":false,"idempotentHint":false,"openWorldHint":false}}]}""",
            answer["result"]!.ToJsonString());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ToolsCall_PassesTheArgumentsAndTheResultThrough()
    {
        var tools = new Tools { Answer = ToolCallResult.Of("2 references.", new JsonObject { ["count"] = 2 }) };
        await using var session = Session.Start(tools);

        await session.SendAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"references","arguments":{"path":"Module1.bas","line":3,"column":5},"_meta":{"progressToken":1}}}""");
        var answer = await session.NextAsync();

        Assert.AreEqual("""{"content":[{"type":"text","text":"2 references."}],"structuredContent":{"count":2},"isError":false}""", answer["result"]!.ToJsonString());
        var call = tools.Calls.Single();
        Assert.AreEqual("references", call.Name);
        Assert.AreEqual("""{"path":"Module1.bas","line":3,"column":5}""", call.Arguments!.ToJsonString());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ToolsCall_ARefusalOfTheTool_IsAResultThatIsAnError()
    {
        await using var session = Session.Start(new Tools { Answer = ToolCallResult.Refusal("No such document.") });

        await session.SendAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"references","arguments":{}}}""");
        var answer = await session.NextAsync();

        Assert.IsNull(answer["error"]);
        Assert.IsTrue((bool)answer["result"]!["isError"]!);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ToolsCall_ThatNamesNoTool_IsInvalidParams()
    {
        var tools = new Tools();
        await using var session = Session.Start(tools);

        await session.SendAsync("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"arguments":{}}}""");
        var answer = await session.NextAsync();

        Assert.AreEqual(McpToolServer.InvalidParams, (int)answer["error"]!["code"]!);
        Assert.IsEmpty(tools.Calls);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ToolsRequests_FailWithInternalError_WhenThePlatformIsNotReadyInTime()
    {
        var tools = new Tools { Ready = new TaskCompletionSource().Task };
        await using var session = Session.Start(tools, new McpLimits { ReadyTimeout = TimeSpan.Zero });

        await session.SendAsync("""{"jsonrpc":"2.0","id":5,"method":"tools/list"}""");
        var answer = await session.NextAsync();

        Assert.AreEqual(McpToolServer.InternalError, (int)answer["error"]!["code"]!);
        Assert.IsFalse(string.IsNullOrEmpty((string)answer["error"]!["message"]!));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ACancelledCall_IsCancelledAndNotAnswered()
    {
        var tools = new Tools { Hold = true };
        await using var session = Session.Start(tools);

        await session.SendAsync("""{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"references"}}""");
        await tools.Started.Task;
        await session.SendAsync("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":7,"reason":"the user stopped it"}}""");
        await tools.Cancelled.Task;
        await session.SendAsync("""{"jsonrpc":"2.0","id":8,"method":"ping"}""");
        var answers = await session.EndAsync();

        Assert.AreEqual("""{"jsonrpc":"2.0","id":8,"result":{}}""", answers.Single().ToJsonString());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TheEndOfInput_CancelsTheCallsStillRunning()
    {
        var tools = new Tools { Hold = true };
        await using var session = Session.Start(tools);

        await session.SendAsync("""{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"references"}}""");
        await tools.Started.Task;
        var answers = await session.EndAsync();

        Assert.IsEmpty(answers);
        Assert.IsTrue(tools.Cancelled.Task.IsCompleted);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AFailureOfTheSource_IsAnInternalErrorWithItsMessage()
    {
        await using var session = Session.Start(new Tools { Failure = new InvalidOperationException("The language server has stopped.") });

        await session.SendAsync("""{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"references"}}""");
        var answer = await session.NextAsync();

        Assert.AreEqual("""{"code":-32603,"message":"The language server has stopped."}""", answer["error"]!.ToJsonString());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task MessagesThatAreNotRequests_AreAnsweredWithTheirError_AndTheSessionGoesOn()
    {
        await using var session = Session.Start(new Tools(), new McpLimits { MaxMessageLength = 200 });

        await session.SendAsync("{not json");
        await session.SendAsync("""[{"jsonrpc":"2.0","id":11,"method":"ping"}]""");
        await session.SendAsync("""{"jsonrpc":"2.0","id":12,"method":"resources/list"}""");
        await session.SendAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await session.SendAsync("{\"jsonrpc\":\"2.0\",\"id\":13,\"method\":\"ping\",\"params\":{\"padding\":\"" + new string('x', 300) + "\"}}");
        await session.SendAsync("""{"jsonrpc":"2.0","id":14,"result":{}}""");
        await session.SendAsync("""{"jsonrpc":"2.0","id":15,"method":"ping"}""");
        var answers = await session.EndAsync();

        CollectionAssert.AreEqual(
            new[] { "null:-32700", "null:-32600", "12:-32601", "null:-32600", "15:ok" },
            answers.Select(answer => $"{answer["id"]?.ToJsonString() ?? "null"}:{(answer["error"] is { } error ? error["code"]!.ToJsonString() : "ok")}").ToArray());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task LinesEndedWithACarriageReturn_AndBlankLines_AreRead()
    {
        await using var session = Session.Start(new Tools());

        await session.SendRawAsync("\r\n\n{\"jsonrpc\":\"2.0\",\"id\":16,\"method\":\"ping\"}\r\n");
        var answer = await session.NextAsync();

        Assert.AreEqual(16, (int)answer["id"]!);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task Errors_AreWordedInTheLanguageOfTheUserInterface()
    {
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
            await using var session = Session.Start(new Tools());

            await session.SendAsync("""{"jsonrpc":"2.0","id":17,"method":"resources/list"}""");
            var answer = await session.NextAsync();

            Assert.AreEqual("Le serveur n'a aucune méthode « resources/list ».", (string)answer["error"]!["message"]!);
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
        }
    }

    [TestMethod]
    public async Task ClientAppToolSource_SendsTheToolsRequestsOverTheAppsConnection()
    {
        var app = Substitute.For<IRDCoreClientApp>();
        app.SendRequestAsync<ToolsListRequest, ToolsListReply>(Arg.Any<ToolsListRequest>(), Arg.Any<CancellationToken>()).Returns(new ToolsListReply(Catalog));
        app.SendRequestAsync<ToolCallRequest, ToolCallReply>(Arg.Any<ToolCallRequest>(), Arg.Any<CancellationToken>()).Returns(new ToolCallReply(ToolCallResult.Of("ok")));
        var source = new ClientAppToolSource(app);

        var listed = await source.ListAsync(CancellationToken.None);
        var answered = await source.CallAsync(new ToolCallParams { Name = CoreAssistantTools.References }, CancellationToken.None);

        Assert.AreEqual(CoreAssistantTools.References, listed.Tools.Single().Name);
        Assert.AreEqual("ok", answered.Content.Single().Text);
        await app.Received(1).SendRequestAsync<ToolCallRequest, ToolCallReply>(
            Arg.Is<ToolCallRequest>(request => request.Name == CoreAssistantTools.References), Arg.Any<CancellationToken>());
    }

    private sealed class Tools : IAssistantToolSource
    {
        public Task Ready { get; init; } = Task.CompletedTask;
        public ToolCallResult Answer { get; init; } = ToolCallResult.Of("ok");
        public Exception? Failure { get; init; }
        public bool Hold { get; init; }
        public List<ToolCallParams> Calls { get; } = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForReadyAsync(CancellationToken cancellationToken) => Ready.WaitAsync(cancellationToken);

        public Task<ToolsListResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(Catalog);

        public async Task<ToolCallResult> CallAsync(ToolCallParams call, CancellationToken cancellationToken)
        {
            lock (Calls)
            {
                Calls.Add(call);
            }

            if (Failure is not null)
            {
                throw Failure;
            }

            if (Hold)
            {
                Started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                finally
                {
                    Cancelled.TrySetResult();
                }
            }

            return Answer;
        }
    }

    // a host on one end of two pipes, the server on the other.
    private sealed class Session : IAsyncDisposable
    {
        private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(30);
        private readonly AnonymousPipeServerStream _toServer = new(PipeDirection.Out);
        private readonly AnonymousPipeServerStream _fromServer = new(PipeDirection.In);
        private readonly AnonymousPipeClientStream _serverInput;
        private readonly AnonymousPipeClientStream _serverOutput;
        private readonly StreamReader _answers;
        private readonly Task _run;
        private bool _ended;

        private Session(IAssistantToolSource tools, McpLimits? limits)
        {
            _serverInput = new AnonymousPipeClientStream(PipeDirection.In, _toServer.ClientSafePipeHandle);
            _serverOutput = new AnonymousPipeClientStream(PipeDirection.Out, _fromServer.ClientSafePipeHandle);
            _answers = new StreamReader(_fromServer, new UTF8Encoding(false));
            var server = new McpToolServer(tools, Server, limits);
            _run = Task.Run(async () =>
            {
                try
                {
                    await server.RunAsync(_serverInput, _serverOutput, CancellationToken.None);
                }
                finally
                {
                    _serverOutput.Dispose();
                }
            });
        }

        public static Session Start(IAssistantToolSource tools, McpLimits? limits = null) => new(tools, limits);

        public Task SendAsync(string message) => SendRawAsync(message + "\n");

        public async Task SendRawAsync(string text)
        {
            await _toServer.WriteAsync(Encoding.UTF8.GetBytes(text));
            await _toServer.FlushAsync();
        }

        public async Task<JsonObject> NextAsync()
            => JsonNode.Parse(await _answers.ReadLineAsync().WaitAsync(HangGuard) ?? throw new AssertFailedException("the server ended"))!.AsObject();

        // closes the host's end, waits for the server to stop, and reads every answer it had not read yet.
        public async Task<IReadOnlyList<JsonObject>> EndAsync()
        {
            _ended = true;
            _toServer.Dispose();
            await _run.WaitAsync(HangGuard);
            var answers = new List<JsonObject>();
            while (await _answers.ReadLineAsync().WaitAsync(HangGuard) is { } line)
            {
                answers.Add(JsonNode.Parse(line)!.AsObject());
            }

            return answers;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_ended)
            {
                _toServer.Dispose();
                await _run.WaitAsync(HangGuard);
            }

            _answers.Dispose();
            _serverInput.Dispose();
        }
    }
}
