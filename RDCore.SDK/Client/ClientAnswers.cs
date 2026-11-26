using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Client;

namespace RDCore.SDK.Client;

/// <summary>
/// How an RDCore client answers the requests a server makes of it.
/// </summary>
public static class ClientAnswers
{
    /// <summary>
    /// Has the client write the error responses it answers a server's requests with as it writes any other response, before it is initialized as after.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LSP allows a server to send <c>window/showMessageRequest</c> while it answers <c>initialize</c> (<strong>LSP 3.17</strong>, Server lifecycle,
    /// Initialize Request), and a server that asks waits for the answer. Until it is initialized, OmniSharp's language client (0.19.9) writes no message
    /// except the <c>initialize</c> request, the <c>initialized</c> notification and a response with a result. It holds an error response, the one it
    /// answers with when its handler fails or when it has no handler for the request, and never writes what it held. The server waits for the answer, the
    /// client waits for the server to answer <c>initialize</c>, and neither moves again.
    /// </para>
    /// <para>
    /// With this, an error response is written as it is answered, and the server is told the request failed: what it does then is the server's to decide,
    /// as it is when a user does not choose. Nothing else the client sends before it is initialized is let through.
    /// </para>
    /// <para>
    /// Every client built on <see cref="RDCoreClientApp"/> is configured with this. A client that builds its OmniSharp options itself calls it.
    /// </para>
    /// </remarks>
    /// <param name="options">The client's options.</param>
    /// <returns>The same options.</returns>
    public static LanguageClientOptions WithErrorAnswersDuringInitialize(this LanguageClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.WithServices(services => services.AddSingleton<IOutputFilter>(ErrorAnswers.Instance));
    }

    // OmniSharp writes what any of its filters lets through, and holds the rest: before the client is initialized, only this one lets an error through.
    private sealed class ErrorAnswers : IOutputFilter
    {
        public static readonly ErrorAnswers Instance = new();

        public bool ShouldOutput(object value) => value is RpcError;
    }
}
