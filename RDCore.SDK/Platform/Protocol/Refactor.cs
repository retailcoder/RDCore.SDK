using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Client;

namespace RDCore.SDK.Platform.Protocol;

// the rdcore/refactor/* requests a client sends, as the platform's JSON-RPC handlers and senders know them: the method each is a request of, and what it is
// answered with. Each is its Protocol DTO and nothing more, and is written as it.

/// <summary>
/// <see cref="RefactorPrepareParams"/> as a request of <c>rdcore/refactor/prepare</c>, answered with a <see cref="RefactorPrepareResult"/>: the type a
/// language server's handler of the request takes.
/// </summary>
[Method(RDCorePlatformProtocol.RefactorPrepare, Direction.ClientToServer)]
public sealed record class RefactorPrepareRequest : RefactorPrepareParams, IRequest, IRequest<RefactorPrepareResult>;

/// <summary>
/// <see cref="RefactorApplyParams"/> as a request of <c>rdcore/refactor/apply</c>, answered with a <see cref="RefactorWorkspaceEdit"/>: the type a language
/// server's handler of the request takes.
/// </summary>
[Method(RDCorePlatformProtocol.RefactorApply, Direction.ClientToServer)]
public sealed record class RefactorApplyRequest : RefactorApplyParams, IRequest, IRequest<RefactorWorkspaceEdit>;

/// <summary>
/// <see cref="RefactorIdentifierCheckParams"/> as a request of <c>rdcore/refactor/checkIdentifier</c>, answered with a
/// <see cref="RefactorIdentifierCheckResult"/>: the type a language server's handler of the request takes.
/// </summary>
[Method(RDCorePlatformProtocol.RefactorCheckIdentifier, Direction.ClientToServer)]
public sealed record class RefactorCheckIdentifierRequest : RefactorIdentifierCheckParams, IRequest, IRequest<RefactorIdentifierCheckResult>;
