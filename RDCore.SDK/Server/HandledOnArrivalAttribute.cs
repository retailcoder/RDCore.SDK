namespace RDCore.SDK.Server;

/// <summary>
/// Marks a request or a notification that an RDCore server handles as it arrives, beside the requests before it that are still being handled, rather than
/// after them.
/// </summary>
/// <remarks>
/// <para>
/// OmniSharp handles a request once those before it are done: a request it runs in order waits for every request before it, and one it runs in parallel waits
/// for those before it that run in order. A request that runs a program - <c>rdcore/host/execute</c>, <c>rdcore/host/debug/resume</c> - is not done until the
/// program waits or is over, and a request that would stop it would wait for it to stop: a program that never ends could not be paused or ended.
/// </para>
/// <para>
/// A request marked with this acts on what another request is doing and is safe to handle while it does: pausing or ending the program that runs, saying which
/// errors it waits at. Its handler must not wait for anything that waits for the request that runs - an unmarked request to another server, the program
/// itself - and may wait only for requests that are marked too: an end the program cannot come to at once is answered after a set while, as asked and still
/// stopping. The marked requests are handled in the order they arrive in, one after the other, so one that waited without bound would hold every one after it;
/// the order of every other request and notification is not changed. A <c>$/cancelRequest</c> does not reach it.
/// </para>
/// <para>
/// A request is marked on its parameters' class. A notification is marked on the class of its handler, since the parameters of a notification of the protocol
/// are OmniSharp's, and is handled on arrival when every handler of it is marked. A notification whose handler begins work that requests wait for is
/// marked, so that a request is never kept waiting for a notification scheduled after it: <c>workspace/didChangeWatchedFiles</c>, whose changes a request
/// about the document waits for (<see cref="IncomingDocumentChanges.ServeFileChanges"/>).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class HandledOnArrivalAttribute : Attribute
{
}
