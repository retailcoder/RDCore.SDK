using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using RDCore.SDK.Client;
using System.IO.Pipelines;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace RDCore.Tests.Client;

/// <summary>
/// What a server asks while it answers <c>initialize</c> is answered, with an error as well (<see cref="ClientAnswers.WithErrorAnswersDuringInitialize"/>):
/// until it is initialized, OmniSharp's language client holds an error response for good, and the server waits for the answer before it answers
/// <c>initialize</c>.
/// </summary>
[TestClass]
public sealed class ClientAnswersTests
{
    // a cap on a hang, not a budget: each wait is on the answer itself, which a loaded machine gives as late as it does.
    private static readonly TimeSpan SafetyCap = TimeSpan.FromMinutes(5);

    // the client's handler fails, and OmniSharp answers InternalError; or it has none, and OmniSharp answers MethodNotFound. Either way the server is told,
    // and answers initialize.
    [TestMethod]
    [Timeout(600_000, CooperativeCancellation = true)]
    [DataRow(true, typeof(InternalErrorException), DisplayName = "a client whose handler fails")]
    [DataRow(false, typeof(MethodNotSupportedException), DisplayName = "a client with no handler")]
    public async Task AnErrorAnswerToWhatTheServerAsksAsItAnswersInitialize_ReachesTheServer_AndInitializeIsAnswered(bool handled, Type expected)
    {
        using var cap = new CancellationTokenSource(SafetyCap);
        var told = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var toServer = new Pipe();
        var toClient = new Pipe();
        var serving = OmniSharpLanguageServer.From(server => server
            .WithInput(toServer.Reader).WithOutput(toClient.Writer)
            .OnInitialize(async (languageServer, _, token) =>
            {
                try
                {
                    await languageServer.Window.ShowMessageRequest(new ShowMessageRequestParams { Type = MessageType.Info, Message = "?" }, token);
                    told.TrySetResult(null);
                }
                catch (Exception exception)
                {
                    told.TrySetResult(exception);
                }
            }), cap.Token);
        var connecting = LanguageClient.From(client =>
        {
            client.WithInput(toClient.Reader).WithOutput(toServer.Writer).WithErrorAnswersDuringInitialize();
            if (handled)
            {
                client.OnShowMessageRequest((ShowMessageRequestParams _, CancellationToken _) =>
                    Task.FromException<MessageActionItem>(new InvalidOperationException("the client's handler fails")));
            }
        }, cap.Token);

        using var client = await connecting.WaitAsync(SafetyCap);
        using var server = await serving.WaitAsync(SafetyCap);

        var answer = await told.Task.WaitAsync(SafetyCap);
        Assert.IsNotNull(answer, "the server was answered with no error: the client no longer answers it with one, and the row needs rewriting.");
        Assert.IsInstanceOfType(answer, expected);
    }
}
