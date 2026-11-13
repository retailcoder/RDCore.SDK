using System.IO.Pipelines;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Server;
using RDCore.SDK.Server;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace RDCore.Tests.Server;

/// <summary>
/// A request an RDCore server or client received is answered however long it runs (<see cref="ServerRequestScheduling.WithNoRequestBudget{T}"/>).
/// OmniSharp gives up on a request that still runs after its timeout, five minutes by default, and answers it <c>RequestCancelled</c>: a diagnostics pull
/// of a large workspace on a loaded machine is told it was cancelled, and its client does not pull again.
/// </summary>
[TestClass]
public sealed class NoRequestBudgetTests
{
    // a cap on a hang, not a budget: each wait is on the answer itself, which a loaded machine gives as late as it does.
    private static readonly TimeSpan SafetyCap = TimeSpan.FromMinutes(5);

    [Method("test/long", Direction.ClientToServer)]
    public sealed record class LongParams : IRequest<int>;

    private sealed class Connected(OmniSharpLanguageServer server, LanguageClient client) : IDisposable
    {
        public LanguageClient Client => client;

        public void Dispose()
        {
            client.Dispose();
            server.Dispose();
        }
    }

    // a server whose test/long request ends when ended does: the handler does nothing else, so how long it runs is the test's to say. Started is set
    // once the handler runs, and told once the handler is told its request is cancelled.
    private static async Task<Connected> ConnectAsync(Action<LanguageServerOptions> configure, Task ended, CancellationToken token,
        TaskCompletionSource? started = null, TaskCompletionSource? told = null)
    {
        var toServer = new Pipe();
        var toClient = new Pipe();
        var serving = OmniSharpLanguageServer.From(server =>
        {
            server.WithInput(toServer.Reader).WithOutput(toClient.Writer)
                .OnRequest<LongParams, int>("test/long", async (_, cancelled) =>
                {
                    started?.TrySetResult();
                    try
                    {
                        await ended.WaitAsync(cancelled);
                    }
                    catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
                    {
                        told?.TrySetResult();
                        throw;
                    }

                    return 42;
                }, new JsonRpcHandlerOptions { RequestProcessType = RequestProcessType.Parallel });
            configure(server);
        }, token);
        var connecting = LanguageClient.From(client => client.WithInput(toClient.Reader).WithOutput(toServer.Writer), token);
        await Task.WhenAll(serving, connecting).WaitAsync(SafetyCap);
        return new Connected(await serving, await connecting);
    }

    // what the timeout does, in OmniSharp: a request that outlives it is answered RequestCancelled, though nobody cancelled it. The row uses a JSON-RPC
    // server, which a language server is built on: a language server's initialize request would be answered RequestCancelled too, with so short a timeout.
    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestThatOutlivesOmniSharpsTimeout_IsAnsweredRequestCancelled()
    {
        using var cap = new CancellationTokenSource(SafetyCap);
        var never = new TaskCompletionSource();
        var toServer = new Pipe();
        var toClient = new Pipe();
        var serving = JsonRpcServer.From(server => server
            .WithInput(toServer.Reader).WithOutput(toClient.Writer)
            .WithMaximumRequestTimeout(TimeSpan.FromMilliseconds(1))
            .OnRequest<LongParams, int>("test/long", async (_, cancelled) =>
            {
                await never.Task.WaitAsync(cancelled);
                return 42;
            }, new JsonRpcHandlerOptions { RequestProcessType = RequestProcessType.Parallel }), cap.Token);
        var connecting = JsonRpcServer.From(client => client.WithInput(toClient.Reader).WithOutput(toServer.Writer), cap.Token);
        await Task.WhenAll(serving, connecting).WaitAsync(SafetyCap);
        using var server = await serving;
        using var client = await connecting;

        _ = await Assert.ThrowsExactlyAsync<RequestCancelledException>(() => client.SendRequest(new LongParams(), cap.Token).WaitAsync(SafetyCap));
    }

    // the budget lifted is one OmniSharp can schedule: a timeout it took for none at all would have answered the request RequestCancelled by the time its
    // handler runs, and one longer than its timer accepts would leave it unanswered.
    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestOfAServerWithEveryRequestAnswered_IsAnsweredItsResult_WhenItEnds()
    {
        using var cap = new CancellationTokenSource(SafetyCap);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var connected = await ConnectAsync(server => server.WithEveryRequestAnswered(), ended.Task, cap.Token, started);

        var answer = connected.Client.SendRequest(new LongParams(), cap.Token);
        await started.Task.WaitAsync(SafetyCap);
        ended.SetResult();

        Assert.AreEqual(42, await answer.WaitAsync(SafetyCap));
    }

    // a sender cancels a request it no longer wants ($/cancelRequest), and the handler is told: with no budget, that is how a request is cancelled.
    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task ARequestOfAServerWithEveryRequestAnswered_ThatItsSenderCancels_IsCancelled()
    {
        using var cap = new CancellationTokenSource(SafetyCap);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var told = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource();
        using var connected = await ConnectAsync(server => server.WithEveryRequestAnswered(), never.Task, cap.Token, started, told);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cap.Token);

        var answer = connected.Client.SendRequest(new LongParams(), cancel.Token);
        await started.Task.WaitAsync(SafetyCap);
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => answer.WaitAsync(SafetyCap));
        await told.Task.WaitAsync(SafetyCap);
    }

    [TestMethod]
    public void AServerWithEveryRequestAnswered_GivesUpOnNothingItReceives()
        => Assert.AreEqual(ServerRequestScheduling.NoRequestBudget, new LanguageServerOptions().WithEveryRequestAnswered().MaximumRequestTimeout);

    // a client's options, as RDCoreClientApp configures them, and any other OmniSharp options.
    [TestMethod]
    public void AClientWithNoRequestBudget_GivesUpOnNothingItReceives()
        => Assert.AreEqual(ServerRequestScheduling.NoRequestBudget, new LanguageClientOptions().WithNoRequestBudget().MaximumRequestTimeout);
}
