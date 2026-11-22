using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace RDCore.SDK.Platform.Protocol;

/// <summary>
/// <c>workspace/willRenameFiles</c> (<strong>LSP 3.17</strong> §Workspace, WillRenameFiles Request): a client is about to rename files or folders, by its
/// user's action or as it applies a workspace edit, and waits for the server's answer before it does.
/// </summary>
/// <remarks>
/// OmniSharp 0.19.9 reads the request as <c>WillRenameFileParams</c>, whose <c>FileRename</c> has only a <c>uri</c>, as it does
/// <see cref="DidRenameWorkspaceFilesParams"/>. This is the same request, read as the protocol states it. A server that handles it says so in its
/// <c>initialize</c> result itself (<c>workspace.fileOperations.willRename</c>), with the filters of the files it is about. A handler of it takes no part in
/// dynamic registration (<see cref="IDoesNotParticipateInRegistration"/>), for the reason <see cref="DidRenameWorkspaceFilesParams"/> gives. Its answer is an
/// edit the client applies before it renames the files, or none.
/// </remarks>
[Method("workspace/willRenameFiles", Direction.ClientToServer)]
public sealed record class WillRenameWorkspaceFilesParams : IRequest<WorkspaceEdit?>, IDoesNotParticipateInRegistration
{
    /// <summary>
    /// The files and folders the client is about to rename, in the order it renames them.
    /// </summary>
    [JsonProperty("files")]
    public WorkspaceFileRename[] Files { get; init; } = [];
}
