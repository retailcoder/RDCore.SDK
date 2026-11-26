using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Client;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Platform.Components;

/// <summary>
/// Request for <c>rdcore/host/discard</c>: the language-server side of <see cref="SessionDiscardParams"/>, addressed to the component that owns the
/// session.
/// </summary>
/// <remarks>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </remarks>
[Method(RDCoreComponentProtocol.HostDiscard, Direction.ClientToServer)]
public record class HostDiscardParams : IRequest, IRequest<DiscardResult>
{
    /// <summary>
    /// The root of the project whose session the module leaves: the root of a project the host composed for analysis
    /// (<see cref="HostProjectAnalyzeParams.WorkspaceRoot"/>) discards the module from that project, and the runtime session is left as it is, as a
    /// define with that root (<see cref="DefineSymbolsParams.WorkspaceRoot"/>) defines the module there. <see langword="null"/>, or the root of the
    /// runtime session's project, discards it from the runtime session.
    /// </summary>
    public Uri? WorkspaceRoot { get; init; }

    /// <summary>
    /// The address of the module symbol: the one the language server defined the module under, which it derives once and for every request about a
    /// module. A module that is not the project's - a document the client opened - has no symbol of its own to find by name, and still has
    /// what it declared.
    /// </summary>
    public Uri ModuleUri { get; init; } = default!;

    /// <summary>
    /// The programmatic name of the module.
    /// </summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the program that is running or waits is ended, and the session wiped, before the module goes. See <see cref="SessionDiscardParams.EndProgram"/>.
    /// </summary>
    public bool EndProgram { get; init; }
}
