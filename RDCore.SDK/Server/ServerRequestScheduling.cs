using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Server;
using RDCore.SDK.Server.Services;

namespace RDCore.SDK.Server;

/// <summary>
/// How an RDCore server schedules the requests and notifications it receives.
/// </summary>
public static class ServerRequestScheduling
{
    /// <summary>
    /// Has the server answer every request it receives, whatever notifications arrive while the request runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OmniSharp runs a request such as <c>textDocument/diagnostic</c> in parallel, and a notification such as <c>textDocument/didOpen</c> in order. By
    /// default, a notification that arrives while requests run abandons them, and answers them <c>ContentModified</c> (-32801). In OmniSharp 0.19.9 a
    /// request whose handling starts just as that happens is answered nothing at all, and its client waits forever, with the server idle: HexIDE's client
    /// saw it on a quick run of <c>didOpen</c>s, each followed by its diagnostics pull (findings desk #31).
    /// </para>
    /// <para>
    /// With this, a request runs to its end and is answered. What it answers for a document that has moved on in the meantime is its handler's to decide:
    /// the diagnostics pull drops a report of a version that is no longer the document's.
    /// </para>
    /// <para>
    /// The notification waits for the requests that run. A request that would make it wait long gives way to a change to its document instead, answered
    /// <c>ContentModified</c> as the change arrives (<see cref="IncomingDocumentChanges.WhileCurrentAsync{T}"/>).
    /// </para>
    /// <para>
    /// Nor is a request given up on because it runs long: it is answered whatever it takes (<see cref="WithNoRequestBudget{T}"/>).
    /// </para>
    /// </remarks>
    /// <param name="options">The server's options.</param>
    /// <returns>The same options.</returns>
    public static LanguageServerOptions WithEveryRequestAnswered(this LanguageServerOptions options)
    {
        options.WithContentModifiedSupport(false);
        return options.WithNoRequestBudget();
    }

    /// <summary>
    /// How long OmniSharp lets a request or a notification that an RDCore server or client received run before it gives up on it: longer than anything
    /// it receives runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OmniSharp gives up on whatever it received that still runs once <see cref="IJsonRpcServerOptions.MaximumRequestTimeout"/> has passed, five minutes
    /// by default. A request is then answered <c>RequestCancelled</c> (-32800), which says that its sender cancelled it, and a notification is dropped.
    /// Five minutes is not long enough for some requests: a diagnostics pull of a large workspace on a loaded machine, or a program that runs, or waits at a
    /// <c>MsgBox</c>, that long. How busy the machine is then decides the answer, and a client that is told its pull was cancelled does not pull again. A
    /// request is cancelled by its sender, which sends <c>$/cancelRequest</c> for it.
    /// </para>
    /// <para>
    /// OmniSharp's timer cannot be turned off. An infinite timeout is taken for none at all, so every request is answered <c>RequestCancelled</c> at
    /// once; and a delay longer than a timer accepts throws, so the request is never answered. This is the longest delay every timer accepts:
    /// <see cref="int.MaxValue"/> milliseconds, about 24.8 days.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan NoRequestBudget = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Sets the options so that OmniSharp lets a request or a notification that a server or a client received run to its end, however long it runs
    /// (<see cref="NoRequestBudget"/>).
    /// </summary>
    /// <remarks>
    /// Every server built on <see cref="RDCoreServerApp"/> and every client built on <see cref="Client.RDCoreClientApp"/> is configured with this. A server or a client
    /// that builds its OmniSharp options itself (a debug adapter, a client of its own) calls it so that what it receives is not answered
    /// <c>RequestCancelled</c> after five minutes.
    /// </remarks>
    /// <typeparam name="T">The options: a language server's, a language client's or a debug adapter's.</typeparam>
    /// <param name="options">The options.</param>
    /// <returns>The same options.</returns>
    public static T WithNoRequestBudget<T>(this T options) where T : IJsonRpcServerOptions
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MaximumRequestTimeout = NoRequestBudget;
        return options;
    }

    /// <summary>
    /// Has the server read what it receives as an RDCore server reads it: an <c>exit</c> notification is let through before the server is initialized,
    /// and once it is, a request marked <see cref="HandledOnArrivalAttribute"/> is handled as it arrives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every server built on <see cref="RDCoreServerApp"/> is configured with this. A server that builds its OmniSharp language server by
    /// itself, and serves requests marked <see cref="HandledOnArrivalAttribute"/>, calls it so that those requests are handled while a request that runs a
    /// program runs: without it they are scheduled after that request, and a program that never ends can be neither paused nor ended.
    /// </para>
    /// <para>
    /// Before the <c>initialize</c> request completes, a request other than <c>initialize</c> is answered <c>ServerNotInitialized</c> (-32002) and a
    /// notification other than <c>exit</c> is dropped (<strong>LSP 3.17</strong>, Server lifecycle, Initialize Request). OmniSharp's own reader of
    /// incoming messages drops an early <c>exit</c> as well, and a client that ends the server before it initializes it waits for a process that does not
    /// end.
    /// </para>
    /// <para>
    /// The <c>locale</c> of the <c>initialize</c> request (<strong>LSP 3.17</strong> <c>InitializeParams.locale</c>) is kept as it arrives: OmniSharp 0.19.9
    /// reads the request without it.
    /// </para>
    /// <para>
    /// OmniSharp keeps the first reader of incoming messages it is given: call this before anything else registers one.
    /// </para>
    /// </remarks>
    /// <param name="options">The server's options.</param>
    /// <returns>The same options.</returns>
    public static LanguageServerOptions WithLifecycleReceiver(this LanguageServerOptions options)
        => options.WithServices(services => services
            .AddSingleton<ArrivalRequests>()
            .AddSingleton<InitializeLocale>()
            .AddSingleton<IReceiver, RDCoreLifecycleReceiver>());
}
