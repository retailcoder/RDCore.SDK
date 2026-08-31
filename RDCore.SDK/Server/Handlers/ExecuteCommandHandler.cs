using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace RDCore.SDK.Server.Handlers;

public class ExecuteCommandHandler() : ExecuteCommandHandlerBase
{
    public async override Task<Unit> Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
    {
        // TODO
        return await Task.FromResult(Unit.Value);
    }

    protected override ExecuteCommandRegistrationOptions CreateRegistrationOptions(ExecuteCommandCapability capability, ClientCapabilities clientCapabilities) 
        => new(){ 
            // TODO
        };
}