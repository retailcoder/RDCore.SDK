using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Platform.Components;

/// <summary>
/// Notification <c>rdcore/host/forms/unloaded</c>: the session has no form loaded any more.
/// </summary>
/// <remarks>
/// Sent by the environment host when the last loaded form of the session is unloaded, or let go of, or ended with the rest of the session's state by an
/// <c>End</c>. A debugger that stayed attached to a program that completed with a form shown (<see cref="ExecuteResult.FormsShown"/>) ends then.
/// <para>
/// A notification and a response travel apart, and the receiver does not see them in the order they were sent: one that arrives before the result it follows is
/// read as nothing. What a receiver acts on is the number of forms loaded (<see cref="SessionStatusResult.FormsLoaded"/>), which it asks once it has the result,
/// and again on each of these.
/// </para>
/// <para>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </para>
/// </remarks>
[Method(RDCoreComponentProtocol.HostFormsUnloaded, Direction.ServerToClient)]
public record class HostFormsUnloadedNotification : IRequest;
