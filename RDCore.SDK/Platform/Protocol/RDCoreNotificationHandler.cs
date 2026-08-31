using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace RDCore.SDK.Platform.Protocol;

/// <summary>
/// The base class for a platform protocol JSON-RPC notification.
/// </summary>
/// <typeparam name="TNotification">A serializable type representing the notification parameters.</typeparam>
/// <remarks>
/// A <c>[Method]</c> attribute must be specified on the implementing class to map the handler to a specific platform protocol notification.
/// </remarks>
public abstract class RDCoreNotificationHandler<TNotification> : IJsonRpcHandler, IJsonRpcNotificationHandler<TNotification>
    where TNotification : IRequest
{
    public async Task<Unit> Handle(TNotification request, CancellationToken cancellationToken)
    {
        await HandleNotificationAsync(request, cancellationToken);
        return Unit.Value;
    }

    protected abstract Task HandleNotificationAsync(TNotification request, CancellationToken token);
}

/// <summary>
/// The base class for a platform protocol JSON-RPC request.
/// </summary>
/// <typeparam name="TNotification">A serializable type representing the request parameters.</typeparam>
/// <remarks>
/// A <c>[Method]</c> attribute must be specified on the implementing class to map the handler to a specific platform protocol request.
/// </remarks>
public abstract class RDCoreRequestHandler<TRequest, TResponse> : IJsonRpcHandler, IJsonRpcRequestHandler<TRequest, TResponse>
    where TRequest : IRequest, IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
        => await HandleAsync(request, cancellationToken);

    protected abstract Task<TResponse> HandleAsync(TRequest request, CancellationToken token);
}
