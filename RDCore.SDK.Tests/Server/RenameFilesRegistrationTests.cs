using System.IO.Pipelines;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Platform.Protocol;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace RDCore.Tests.Server;

/// <summary>
/// A server that handles <c>workspace/didRenameFiles</c> and <c>workspace/willRenameFiles</c> (<see cref="DidRenameWorkspaceFilesParams"/>,
/// <see cref="WillRenameWorkspaceFilesParams"/>) does not ask a client to register them dynamically. OmniSharp would register them with no options, and
/// VS Code's client throws on the missing <c>filters</c> and drops every registration after them in the same request, completion among them.
/// </summary>
[TestClass]
public sealed class RenameFilesRegistrationTests
{
    private sealed class DidRename : IJsonRpcNotificationHandler<DidRenameWorkspaceFilesParams>
    {
        public Task<Unit> Handle(DidRenameWorkspaceFilesParams request, CancellationToken cancellationToken) => Unit.Task;
    }

    private sealed class WillRename : IJsonRpcRequestHandler<WillRenameWorkspaceFilesParams, WorkspaceEdit?>
    {
        public Task<WorkspaceEdit?> Handle(WillRenameWorkspaceFilesParams request, CancellationToken cancellationToken) =>
            Task.FromResult<WorkspaceEdit?>(null);
    }

    // the methods a client that registers file operations and completion dynamically, as VS Code does, is asked to register by a server that handles both
    // renames and completion. The server sends its registrations in one request, so they are all there once completion is.
    private static async Task<IReadOnlyList<string>> RegisteredMethodsAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var toServer = new Pipe();
        var toClient = new Pipe();
        var serving = OmniSharpLanguageServer.From(server => server
            .WithInput(toServer.Reader)
            .WithOutput(toClient.Writer)
            .AddHandler(new DidRename())
            .AddHandler(new WillRename())
            .OnCompletion((_, _, _) => Task.FromResult(new CompletionList()), (_, _) => new CompletionRegistrationOptions()),
            timeout.Token);
        var connecting = LanguageClient.From(client => client
            .WithInput(toClient.Reader)
            .WithOutput(toServer.Writer)
            .WithCapability(new FileOperationsWorkspaceClientCapabilities { DynamicRegistration = true, DidRename = true, WillRename = true })
            .WithCapability(new CompletionCapability { DynamicRegistration = true }), timeout.Token);
        await Task.WhenAll(serving, connecting).WaitAsync(TimeSpan.FromSeconds(20));
        using var server = await serving;
        using var client = await connecting;

        var registrations = await client.RegistrationManager.Registrations
            .Select(current => current.ToList())
            .FirstAsync(current => current.Any(registration => registration.Method == "textDocument/completion"))
            .ToTask(timeout.Token);
        return registrations.Select(registration => registration.Method).ToList();
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AServerThatHandlesRenames_DoesNotRegisterThemDynamically()
    {
        var methods = await RegisteredMethodsAsync();

        CollectionAssert.DoesNotContain(methods.ToList(), "workspace/didRenameFiles");
        CollectionAssert.DoesNotContain(methods.ToList(), "workspace/willRenameFiles");
    }
}
