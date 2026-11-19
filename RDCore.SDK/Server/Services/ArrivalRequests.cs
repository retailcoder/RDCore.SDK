using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.JsonRpc.Server.Messages;

namespace RDCore.SDK.Server.Services;

/// <summary>
/// Handles the requests marked <see cref="HandledOnArrivalAttribute"/> as they arrive, beside the requests OmniSharp is handling in its order.
/// </summary>
/// <remarks>
/// <para>
/// OmniSharp hands every request it receives to a scheduler that handles it once those before it are done, and builds that scheduler itself: a server cannot
/// give it another. What it receives is first read by the server's <see cref="IReceiver"/> (<see cref="RDCoreLifecycleReceiver"/>), and a request the receiver
/// keeps is never scheduled: it is handled here, through the same handlers, and answered as OmniSharp answers a request.
/// </para>
/// <para>
/// Whether a request is marked is a fact of its method, and its parameters are read only when it is: the receiver reads what arrives outside the
/// <c>try</c> in which OmniSharp answers a request whose parameters can't be read, and such a request is left to OmniSharp, which answers it so.
/// </para>
/// <para>
/// The marked requests are handled one after the other, in the order they arrived in, and each is begun as it is read - before the message after it is: a
/// pause sent before a run is not a pause of that run. A <c>$/cancelRequest</c> does not reach a request handled here: OmniSharp knows of the requests it
/// schedules.
/// </para>
/// <para>
/// A notification is marked by its handlers' classes, every one of them: the parameters of a notification of the protocol are OmniSharp's. A marked
/// notification is handled in the same order as the marked requests.
/// </para>
/// </remarks>
/// <param name="services">The server's services: its router of requests and notifications, its handlers, and its output.</param>
/// <param name="logger">Logs a request or a notification whose parameters cannot be read, and one whose handler fails.</param>
internal sealed class ArrivalRequests(IServiceProvider services, ILogger<ArrivalRequests> logger)
{
    private readonly Lazy<IRequestRouter<IHandlerDescriptor?>> _router = new(services.GetRequiredService<IRequestRouter<IHandlerDescriptor?>>);
    private readonly Lazy<IHandlersManager> _handlers = new(services.GetRequiredService<IHandlersManager>);
    private readonly Lazy<IOutputHandler> _output = new(services.GetRequiredService<IOutputHandler>);
    private readonly object _gate = new();
    private Task _last = Task.CompletedTask;

    /// <summary>
    /// Handles <paramref name="request"/> if it is marked, and answers it once its handler is done.
    /// </summary>
    /// <param name="request">A request the server received.</param>
    /// <returns>Whether the request is marked, and so is handled here rather than scheduled.</returns>
    public bool TryHandle(Request request)
    {
        if (!IsMarked(request.Method))
        {
            return false;
        }

        IRequestDescriptor<IHandlerDescriptor?> descriptor;
        try
        {
            descriptor = _router.Value.GetDescriptors(request);
        }
        catch (Exception exception)
        {
            logger.LogWarning("The parameters of {Method} {RequestId} can't be read ({Reason}); the request is left to be answered so.", request.Method, request.Id, exception.Message);
            return false;
        }

        lock (_gate)
        {
            _last = AnswerAfterAsync(_last, descriptor, request);
        }

        return true;
    }

    /// <summary>
    /// Handles <paramref name="notification"/> if every handler of it is marked (<see cref="HandledOnArrivalAttribute"/> on the handler's class).
    /// </summary>
    /// <param name="notification">A notification the server received.</param>
    /// <returns>Whether the notification is marked, and so is handled here rather than scheduled.</returns>
    public bool TryHandle(Notification notification)
    {
        // a notification of the protocol has parameters OmniSharp declares: its handler is what is marked.
        var handlers = _handlers.Value.Descriptors.Where(handler => handler.Method == notification.Method).ToList();
        if (handlers.Count == 0 || !handlers.All(handler => handler.ImplementationType.IsDefined(typeof(HandledOnArrivalAttribute), inherit: true)))
        {
            return false;
        }

        IRequestDescriptor<IHandlerDescriptor?> descriptor;
        try
        {
            descriptor = _router.Value.GetDescriptors(notification);
        }
        catch (Exception exception)
        {
            logger.LogWarning("The parameters of {Method} can't be read ({Reason}); the notification is left to OmniSharp.", notification.Method, exception.Message);
            return false;
        }

        lock (_gate)
        {
            _last = HandleAfterAsync(_last, descriptor, notification);
        }

        return true;
    }

    // a delegating handler's parameters are the request it delegates, which is the one that is marked.
    private bool IsMarked(string method)
        => _handlers.Value.Descriptors.FirstOrDefault(handler => handler.Method == method) is { Params: { } parameters } handler
            && (handler.IsDelegatingHandler ? parameters.GetGenericArguments().FirstOrDefault() ?? parameters : parameters)
                .IsDefined(typeof(HandledOnArrivalAttribute), inherit: true);

    private async Task AnswerAfterAsync(Task previous, IRequestDescriptor<IHandlerDescriptor?> descriptor, Request request)
    {
        await previous;
        await AnswerAsync(descriptor, request);
    }

    // a notification is answered nothing; one whose handler fails is logged, and never faults, so that what arrived after it is handled.
    private async Task HandleAfterAsync(Task previous, IRequestDescriptor<IHandlerDescriptor?> descriptor, Notification notification)
    {
        await previous;
        try
        {
            await _router.Value.RouteNotification(descriptor, notification, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "❌ The notification {Method} failed.", notification.Method);
        }
    }

    // answered as OmniSharp answers a request it schedules; never faults, so that the requests after it are handled.
    private async Task AnswerAsync(IRequestDescriptor<IHandlerDescriptor?> descriptor, Request request)
    {
        ErrorResponse response;
        try
        {
            response = await _router.Value.RouteRequest(descriptor, request, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            response = new RequestCancelled(request.Id, request.Method);
        }
        catch (RpcErrorException exception)
        {
            response = new RpcError(request.Id, request.Method, new ErrorMessage(exception.Code, exception.Message, exception.Error));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "❌ The request {Method} {RequestId} failed.", request.Method, request.Id);
            response = new InternalError(request.Id, request.Method, ServerErrorAnswers.OneLineOf(exception));
        }

        try
        {
            _output.Value.Send(response.Value);
        }
        catch (Exception exception)
        {
            logger.LogWarning("The answer to {Method} {RequestId} was not sent ({Reason}).", request.Method, request.Id, exception.Message);
        }
    }
}
