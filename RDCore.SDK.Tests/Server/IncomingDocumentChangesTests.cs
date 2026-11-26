using System.IO.Pipelines;
using System.Text;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Client;
using RDCore.SDK.Server;
using RDCore.SDK.Workspace;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace RDCore.Tests.Server;

/// <summary>
/// A request about a document stops when a change to the document arrives, and is answered <c>ContentModified</c>, so that the change, and the client's next
/// request, don't wait for it (<see cref="IncomingDocumentChanges"/>): HexIDE's completion waited five seconds behind a diagnostics pull (findings desk #31).
/// </summary>
[TestClass]
public sealed class IncomingDocumentChangesTests
{
    private const string DocumentA = "file:///c:/ws/A.bas";
    private const string DocumentB = "file:///c:/ws/B.bas";

    public sealed record class Document(string Uri);

    [Method("test/aboutDocument", Direction.ClientToServer)]
    public sealed record class AboutDocumentParams(Document TextDocument) : IRequest<int>;

    [Method("textDocument/didChange", Direction.ClientToServer)]
    public sealed record class ChangeParams(Document TextDocument) : IRequest;

    // a server whose request about a document goes on until it is let go (heeding its token or not), and that applies a change to a document in order, as
    // didChange is. A change that waited for the request would never be applied before the test let it go.
    private sealed class Wire : IAsyncDisposable
    {
        private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(30));
        private OmniSharpLanguageServer? _server;

        public IncomingDocumentChanges Incoming { get; } = new();
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Applied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LanguageClient Client { get; private set; } = default!;

        public static async Task<Wire> StartAsync(bool answerHeedsItsToken)
        {
            var wire = new Wire();
            var toServer = new Pipe();
            var toClient = new Pipe();
            var serving = OmniSharpLanguageServer.From(server => server
                .WithInput(PipeReader.Create(wire.Incoming.Tap(toServer.Reader.AsStream())))
                .WithOutput(toClient.Writer)
                .WithEveryRequestAnswered()
                .OnRequest<AboutDocumentParams, int>("test/aboutDocument", (request, token) => wire.Incoming.WhileCurrentAsync(new Uri(request.TextDocument.Uri), token, async answering =>
                {
                    wire.Started.TrySetResult();
                    await wire.Release.Task.WaitAsync(answerHeedsItsToken ? answering : CancellationToken.None);
                    return 42;
                }), new JsonRpcHandlerOptions { RequestProcessType = RequestProcessType.Parallel })
                .OnNotification<ChangeParams>("textDocument/didChange", change =>
                {
                    wire.Incoming.Applied(new Uri(change.TextDocument.Uri));
                    wire.Applied.TrySetResult();
                }, new JsonRpcHandlerOptions { RequestProcessType = RequestProcessType.Serial }),
                wire._timeout.Token);
            var connecting = LanguageClient.From(client => client.WithInput(toClient.Reader).WithOutput(toServer.Writer), wire._timeout.Token);
            await Task.WhenAll(serving, connecting).WaitAsync(TimeSpan.FromSeconds(20));
            wire._server = await serving;
            wire.Client = await connecting;
            return wire;
        }

        public async ValueTask DisposeAsync()
        {
            Release.TrySetResult();
            Client.Dispose();
            _server?.Dispose();
            _timeout.Dispose();
            await Task.CompletedTask;
        }
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ARequestAboutADocument_WhenAChangeToItArrives_IsAnsweredContentModifiedAtOnce()
    {
        await using var wire = await Wire.StartAsync(answerHeedsItsToken: true);

        var answer = wire.Client.SendRequest(new AboutDocumentParams(new Document(DocumentA)), CancellationToken.None);
        await wire.Started.Task.WaitAsync(TimeSpan.FromSeconds(20));
        wire.Client.SendNotification(new ChangeParams(new Document(DocumentA)));

        await Assert.ThrowsExactlyAsync<ContentModifiedException>(() => answer.WaitAsync(TimeSpan.FromSeconds(20)));
        await wire.Applied.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.IsFalse(wire.Release.Task.IsCompleted, "the change was applied while the request still went on");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ARequestAboutADocument_WhenAChangeToAnotherArrives_IsAnsweredItsResult()
    {
        await using var wire = await Wire.StartAsync(answerHeedsItsToken: true);

        var answer = wire.Client.SendRequest(new AboutDocumentParams(new Document(DocumentA)), CancellationToken.None);
        await wire.Started.Task.WaitAsync(TimeSpan.FromSeconds(20));
        // the change, which is applied in order, waits for the request that runs meanwhile: the request is let go once the change is sent.
        wire.Client.SendNotification(new ChangeParams(new Document(DocumentB)));
        wire.Release.SetResult();

        Assert.AreEqual(42, await answer.WaitAsync(TimeSpan.FromSeconds(20)));
        await wire.Applied.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ARequestWhoseAnswerDoesNotHeedItsToken_StillEndsWhenAChangeArrives()
    {
        await using var wire = await Wire.StartAsync(answerHeedsItsToken: false);

        var answer = wire.Client.SendRequest(new AboutDocumentParams(new Document(DocumentA)), CancellationToken.None);
        await wire.Started.Task.WaitAsync(TimeSpan.FromSeconds(20));
        wire.Client.SendNotification(new ChangeParams(new Document(DocumentA)));

        await Assert.ThrowsExactlyAsync<ContentModifiedException>(() => answer.WaitAsync(TimeSpan.FromSeconds(20)));
        await wire.Applied.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.IsFalse(wire.Release.Task.IsCompleted, "the change was applied while the request, which does not heed its token, still went on");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ARequestTheClientCancels_EndsAtOnce_ThoughItsAnswerDoesNotHeedItsToken()
    {
        await using var wire = await Wire.StartAsync(answerHeedsItsToken: false);
        using var cancel = new CancellationTokenSource();

        var answer = wire.Client.SendRequest(new AboutDocumentParams(new Document(DocumentA)), cancel.Token);
        await wire.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancel.CancelAsync();
        wire.Client.SendNotification(new ChangeParams(new Document(DocumentA)));

        await Assert.ThrowsAsync<OperationCanceledException>(() => answer.WaitAsync(TimeSpan.FromSeconds(20)));
        await wire.Applied.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.IsFalse(wire.Release.Task.IsCompleted, "the change was applied while the cancelled request, which does not heed its token, still went on");
    }

    [TestMethod]
    public async Task ARequestThatStartsWhileAChangeToItsDocumentIsUnapplied_IsContentModified()
    {
        var incoming = new IncomingDocumentChanges();
        incoming.Arrived(new Uri(DocumentA));

        Assert.IsTrue(await IsUnappliedAsync(incoming, DocumentA));
        Assert.AreEqual(42, await incoming.WhileCurrentAsync(new Uri(DocumentB), CancellationToken.None, _ => Task.FromResult(42)), "another document is current");

        incoming.Applied(new Uri(DocumentA));
        Assert.AreEqual(42, await incoming.WhileCurrentAsync(new Uri(DocumentA), CancellationToken.None, _ => Task.FromResult(42)), "the change is applied");
    }

    // a request made of every document, such as workspace/diagnostic, is given up for a change to any of them.
    [TestMethod]
    public async Task ARequestAboutTheWorkspace_IsContentModified_ByAChangeToAnyDocument()
    {
        var incoming = new IncomingDocumentChanges();
        Assert.AreEqual(42, await incoming.WhileWorkspaceCurrentAsync(CancellationToken.None, _ => Task.FromResult(42)), "nothing is unapplied");

        incoming.Arrived(new Uri(DocumentB));
        await Assert.ThrowsAsync<RpcErrorException>(() => incoming.WhileWorkspaceCurrentAsync(CancellationToken.None, _ => Task.FromResult(42)),
            "a change unapplied as it starts");

        incoming.Applied(new Uri(DocumentB));
        var started = new TaskCompletionSource();
        var running = incoming.WhileWorkspaceCurrentAsync(CancellationToken.None, async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        });
        await started.Task;
        incoming.Arrived(new Uri(DocumentA));

        var modified = await Assert.ThrowsAsync<RpcErrorException>(() => running.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.AreEqual(-32801, modified.Code, "ContentModified: a change that arrives while it runs");
    }

    // a client opens a README.md beside the modules: OmniSharp hands its didOpen to no handler, so it is never applied. Waited for, every workspace/diagnostic
    // after it was answered ContentModified, and the client pulled again, for good.
    [TestMethod]
    public async Task AChangeToADocumentNoHandlerServes_IsNotWaitedFor()
    {
        var incoming = new IncomingDocumentChanges();
        incoming.Serve(SupportedLanguageSelectors.DocumentSelector);

        incoming.Arrived(new Uri("file:///c:/ws/README.md"));

        Assert.AreEqual(42, await incoming.WhileWorkspaceCurrentAsync(CancellationToken.None, _ => Task.FromResult(42)));
        Assert.IsFalse(await IsUnappliedAsync(incoming, "file:///c:/ws/README.md"));
    }

    [TestMethod]
    [DataRow("file:///c:/ws/Module1.bas")]
    [DataRow("file:///c:/ws/Classes/Class1.cls")]
    [DataRow("file:///c:/ws/Form1.frm")]
    [DataRow("file:///c:/ws/Program1.rdc")]
    public async Task AChangeToADocumentAHandlerServes_IsWaitedFor(string document)
    {
        var incoming = new IncomingDocumentChanges();
        incoming.Serve(SupportedLanguageSelectors.DocumentSelector);

        incoming.Arrived(new Uri(document));

        await Assert.ThrowsAsync<RpcErrorException>(() => incoming.WhileWorkspaceCurrentAsync(CancellationToken.None, _ => Task.FromResult(42)));
    }

    [TestMethod]
    public async Task AChangeAppliedThatNeverArrived_OwesNothing()
    {
        var incoming = new IncomingDocumentChanges();
        incoming.Applied(new Uri(DocumentA));
        incoming.Arrived(new Uri(DocumentA));
        incoming.Applied(new Uri(DocumentA));

        Assert.AreEqual(42, await incoming.WhileCurrentAsync(new Uri(DocumentA), CancellationToken.None, _ => Task.FromResult(42)));
    }

    private static readonly TimeSpan Hung = TimeSpan.FromSeconds(20);

    private static IncomingDocumentChanges ServingFileChanges()
    {
        var incoming = new IncomingDocumentChanges();
        incoming.ServeFileChanges();
        return incoming;
    }

    [TestMethod]
    public async Task ARequestAboutADocument_WaitsForTheChangesToItsFileThatArrivedBeforeIt()
    {
        var incoming = ServingFileChanges();
        incoming.FileChangeArrived(new Uri(DocumentA));
        incoming.FileChangeArrived(new Uri(DocumentA));

        var answer = incoming.WhileCurrentAsync(new Uri(DocumentA), CancellationToken.None, _ => Task.FromResult(42));
        Assert.AreEqual(7, await incoming.WhileCurrentAsync(new Uri(DocumentB), CancellationToken.None, _ => Task.FromResult(7)).WaitAsync(Hung),
            "a request about another document does not wait");
        incoming.FileChangeRead(new Uri(DocumentA));
        Assert.IsFalse(answer.IsCompleted, "one change to the file is not read");

        incoming.FileChangeRead(new Uri(DocumentA));
        Assert.AreEqual(42, await answer.WaitAsync(Hung));
    }

    [TestMethod]
    public async Task ARequestAboutTheWorkspace_WaitsForTheChangesToEveryFile()
    {
        var incoming = ServingFileChanges();
        incoming.FileChangeArrived(new Uri(DocumentA));
        incoming.FileChangeArrived(new Uri(DocumentB));

        var answer = incoming.WhileWorkspaceCurrentAsync(CancellationToken.None, _ => Task.FromResult(42));
        incoming.FileChangeRead(new Uri(DocumentA));
        Assert.IsFalse(answer.IsCompleted, "B's file is not read");

        incoming.FileChangeRead(new Uri(DocumentB));
        Assert.AreEqual(42, await answer.WaitAsync(Hung));
    }

    // what is waited for is what arrived before the wait was asked for; a request reads the changes that arrive while it waits too.
    [TestMethod]
    public async Task AWaitForAFile_IsForTheChangesThatArrivedBeforeIt()
    {
        var incoming = ServingFileChanges();
        incoming.FileChangeArrived(new Uri(DocumentA));
        var read = incoming.WhenFileChangesReadAsync(new Uri(DocumentA), CancellationToken.None);
        var answer = incoming.WhileCurrentAsync(new Uri(DocumentA), CancellationToken.None, _ => Task.FromResult(42));
        incoming.FileChangeArrived(new Uri(DocumentA));

        incoming.FileChangeRead(new Uri(DocumentA));
        await read.WaitAsync(Hung);
        Assert.IsFalse(answer.IsCompleted, "the request waits for the change that arrived while it waited");

        incoming.FileChangeRead(new Uri(DocumentA));
        Assert.AreEqual(42, await answer.WaitAsync(Hung));
    }

    [TestMethod]
    public async Task ARequestAboutADocument_WhenAChangeToItsFileArrives_IsAnsweredContentModified()
    {
        var incoming = ServingFileChanges();
        var started = new TaskCompletionSource();
        var running = incoming.WhileCurrentAsync(new Uri(DocumentA), CancellationToken.None, async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        });
        await started.Task.WaitAsync(Hung);

        incoming.FileChangeArrived(new Uri(DocumentA));

        var modified = await Assert.ThrowsAsync<RpcErrorException>(() => running.WaitAsync(Hung));
        Assert.AreEqual(ErrorCodes.ContentModified, modified.Code);
    }

    // a server that does not say it reads them never says a change is read: waited for, its requests would wait for good.
    [TestMethod]
    public async Task AChangeToAFile_IsNotWaitedFor_WhenTheServerDoesNotServeFileChanges_OrTheDocument()
    {
        var notServing = new IncomingDocumentChanges();
        notServing.FileChangeArrived(new Uri(DocumentA));
        Assert.AreEqual(42, await notServing.WhileCurrentAsync(new Uri(DocumentA), CancellationToken.None, _ => Task.FromResult(42)).WaitAsync(Hung));

        var serving = ServingFileChanges();
        serving.Serve(SupportedLanguageSelectors.DocumentSelector);
        serving.FileChangeArrived(new Uri("file:///c:/ws/.rdproj"));
        serving.FileChangeArrived(new Uri("file:///c:/ws/README.md"));
        Assert.AreEqual(42, await serving.WhileWorkspaceCurrentAsync(CancellationToken.None, _ => Task.FromResult(42)).WaitAsync(Hung));
    }

    [TestMethod]
    public void AChangeToAFileReadThatNeverArrived_OwesNothing()
    {
        var incoming = ServingFileChanges();
        incoming.FileChangeRead(new Uri(DocumentA));
        incoming.FileChangeArrived(new Uri(DocumentA));
        Assert.IsFalse(incoming.WhenFileChangesReadAsync(new Uri(DocumentA), CancellationToken.None).IsCompleted);

        incoming.FileChangeRead(new Uri(DocumentA));
        incoming.FileChangeRead(new Uri(DocumentA));

        Assert.IsTrue(incoming.WhenFileChangesReadAsync(new Uri(DocumentA), CancellationToken.None).IsCompleted);
    }

    [TestMethod]
    public async Task AWaitForAFile_IsCancelledWithItsToken()
    {
        var incoming = ServingFileChanges();
        incoming.FileChangeArrived(new Uri(DocumentA));
        using var cancel = new CancellationTokenSource();
        var answer = incoming.WhileCurrentAsync(new Uri(DocumentA), cancel.Token, _ => Task.FromResult(42));

        await cancel.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => answer.WaitAsync(Hung));
    }

    private static string Framed(string json) => $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";

    // what the tap reads, in reads of the given sizes, is passed on byte for byte; the changes it saw are said.
    private static (byte[] PassedOn, IncomingDocumentChanges Incoming) ReadThrough(string wire, int readSize, IncomingDocumentChanges? to = null)
    {
        var incoming = to ?? new IncomingDocumentChanges();
        using var tap = incoming.Tap(new MemoryStream(Encoding.UTF8.GetBytes(wire)));
        using var passedOn = new MemoryStream();
        var buffer = new byte[readSize];
        int read;
        while ((read = tap.Read(buffer, 0, buffer.Length)) > 0)
        {
            passedOn.Write(buffer, 0, read);
        }
        return (passedOn.ToArray(), incoming);
    }

    // a request about the document, made now, is answered ContentModified: the error OmniSharp writes with that code.
    private static async Task<bool> IsUnappliedAsync(IncomingDocumentChanges incoming, string document)
    {
        try
        {
            await incoming.WhileCurrentAsync(new Uri(document), CancellationToken.None, _ => Task.FromResult(0));
            return false;
        }
        catch (RpcErrorException error) when (error.Code == ErrorCodes.ContentModified)
        {
            return true;
        }
    }

    [TestMethod]
    [DataRow(1, DisplayName = "a byte at a time")]
    [DataRow(7, DisplayName = "7 bytes at a time")]
    [DataRow(4096, DisplayName = "all at once")]
    public async Task TheTap_PassesEveryByteOn_AndSeesEachChangeToADocument(int readSize)
    {
        var wire = Framed("""{"jsonrpc":"2.0","id":1,"method":"textDocument/diagnostic","params":{"textDocument":{"uri":"file:///c:/ws/B.bas"}}}""")
            + "Content-Type: application/vscode-jsonrpc; charset=utf-8\r\n" + Framed("""{"jsonrpc":"2.0","method":"textDocument/didChange","params":{"textDocument":{"uri":"file:///c:/ws/A.bas","version":2},"contentChanges":[{"text":"é"}]}}""")
            + Framed("""{"jsonrpc":"2.0","method":"textDocument/didSave","params":{"textDocument":{"uri":"file:///c:/ws/C.bas"}}}""");

        var (passedOn, incoming) = ReadThrough(wire, readSize);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(wire), passedOn);
        Assert.IsTrue(await IsUnappliedAsync(incoming, DocumentA), "didChange changes A");
        Assert.IsFalse(await IsUnappliedAsync(incoming, DocumentB), "a pull changes nothing");
        Assert.IsFalse(await IsUnappliedAsync(incoming, "file:///c:/ws/C.bas"), "didSave changes nothing");
    }

    [TestMethod]
    [DataRow(1, DisplayName = "a byte at a time")]
    [DataRow(4096, DisplayName = "all at once")]
    public void TheTap_SeesEachFileTheClientSaysChanged(int readSize)
    {
        var wire = Framed("""{"jsonrpc":"2.0","method":"workspace/didChangeWatchedFiles","params":{"changes":[{"uri":"file:///c:/ws/A.bas","type":2},{"uri":"file:///c:/ws/B.bas","type":3}]}}""")
            + Framed("""{"jsonrpc":"2.0","method":"workspace/didChangeConfiguration","params":{"settings":{"uri":"file:///c:/ws/C.bas"}}}""");

        var (passedOn, incoming) = ReadThrough(wire, readSize, ServingFileChanges());

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(wire), passedOn);
        Assert.IsFalse(incoming.WhenFileChangesReadAsync(new Uri(DocumentA), CancellationToken.None).IsCompleted, "A's file changed");
        Assert.IsFalse(incoming.WhenFileChangesReadAsync(new Uri(DocumentB), CancellationToken.None).IsCompleted, "B's file was deleted");
        Assert.IsTrue(incoming.WhenFileChangesReadAsync(new Uri("file:///c:/ws/C.bas"), CancellationToken.None).IsCompleted, "a setting is not a file");
    }

    [TestMethod]
    public async Task TheTap_PassesOnWhatItCannotRead_AndSeesTheMessagesAfterIt()
    {
        var wire = Framed("""{"method":"textDocument/didOpen", not json""") + Framed("""{"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///c:/ws/A.bas","languageId":"vba","version":1,"text":""}}}""");

        var (passedOn, incoming) = ReadThrough(wire, 5);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(wire), passedOn);
        Assert.IsTrue(await IsUnappliedAsync(incoming, DocumentA));
    }
}
