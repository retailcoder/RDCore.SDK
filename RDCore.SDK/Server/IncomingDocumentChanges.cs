using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace RDCore.SDK.Server;

/// <summary>
/// The changes to documents that the client has sent and the server has not applied yet: a request about a document one of them changes is answered
/// <c>ContentModified</c> (-32801) at once, instead of keeping the change, and every request the client sends after it, waiting until it ends.
/// </summary>
/// <remarks>
/// <para>
/// OmniSharp runs a notification that changes a document (<c>textDocument/didOpen</c>, <c>didChange</c>, <c>didClose</c>) in order, after the requests already
/// running, which it runs in parallel. An RDCore server answers every request (<see cref="ServerRequestScheduling.WithEveryRequestAnswered"/>), so a keystroke
/// that arrives while the document's diagnostics are being pulled waits for the pull, and so does the completion the client sends after it: on a module of
/// 2,100 lines, about five seconds (HexIDE's findings desk #31).
/// </para>
/// <para>
/// The server knows of a change as it arrives (<see cref="Tap"/>), long before its turn comes. A request made of what the document was says so with
/// <see cref="WhileCurrentAsync{T}"/>: when a change to its document arrives while it runs, or has arrived and is not applied yet when it starts, the request
/// stops and is answered <c>ContentModified</c>, which LSP 3.17 §Response Message has a server answer "if it detects a content change in its unprocessed
/// messages". The client asks again for the document as it is now. A change to another document changes nothing for it.
/// </para>
/// <para>
/// A change to a document's file that the client says of (<c>workspace/didChangeWatchedFiles</c>) is read by a server that serves file changes
/// (<see cref="ServeFileChanges"/>) as the notification arrives, beside the requests that run. A request about the document waits until every such change that
/// arrived before it is read (<see cref="WhenFileChangesReadAsync"/>), and is then made of the document as the file is.
/// </para>
/// </remarks>
public sealed class IncomingDocumentChanges
{
    // what the error answers: the protocol's own words for it (LSP 3.17 §Response Message), which a client may show, but more likely asks again.
    private const string ContentModifiedMessage = "Content Modified";

    private readonly object _gate = new();
    private readonly Dictionary<string, int> _unapplied = new(StringComparer.OrdinalIgnoreCase);

    // what each running request is made of: one document, or every document of the workspace (null).
    private readonly List<(string? Document, CancellationTokenSource Changed)> _watches = [];

    // the documents a handler applies a change to: every one, until the server says which.
    private volatile Func<Uri, bool> _serves = static _ => true;

    // the changes to documents' files that arrived and are not read yet, and the requests that wait for them; none until the server serves them.
    private readonly Dictionary<string, FileChanges> _files = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _servesFileChanges;

    // how many changes to a document's file arrived and how many were read, and how many must be read for each request that waits to go on.
    private sealed class FileChanges
    {
        public long Arrived { get; set; }
        public long Read { get; set; }
        public List<(long Until, TaskCompletionSource Read)> Waiting { get; } = [];
    }

    /// <summary>
    /// Says which documents the server's handlers apply a change to (<see cref="Applied"/>). A change to any other is no change of the server's: OmniSharp hands
    /// it to no handler, so it is never applied, and is not waited for.
    /// </summary>
    /// <param name="serves">Whether a handler applies a change to a document.</param>
    public void Serve(Func<Uri, bool> serves) => _serves = serves;

    /// <summary>
    /// Says that the server's handlers apply a change to the documents a selector matches, which is what they are registered for: the client names a
    /// document's language its own way, so a filter of the selector is matched by the document's path alone.
    /// </summary>
    /// <param name="selector">What the handlers of a document's notifications are registered for.</param>
    public void Serve(TextDocumentSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        Serve(document => selector.IsMatch(new TextDocumentAttributes(document, string.Empty)));
    }

    /// <summary>
    /// A change to a document has arrived from the client, and is waiting for its turn: the requests about the document that are running are answered
    /// <c>ContentModified</c>, and so is each that starts before the change is applied. A change to a document the server does not serve
    /// (<see cref="Serve(Func{Uri, bool})"/>) is nothing.
    /// </summary>
    /// <param name="document">The document the change is to.</param>
    public void Arrived(Uri document)
    {
        if (!_serves(document))
        {
            return;
        }

        var key = Key(document);
        CancellationTokenSource[] changed;
        lock (_gate)
        {
            _unapplied[key] = _unapplied.GetValueOrDefault(key) + 1;
            changed = Watching(key);
        }

        foreach (var source in changed)
        {
            source.Cancel();
        }
    }

    // the requests that run, made of the document: those about it, and those about every document.
    private CancellationTokenSource[] Watching(string key)
        => [.. _watches.Where(watch => watch.Document is null || string.Equals(watch.Document, key, StringComparison.OrdinalIgnoreCase)).Select(watch => watch.Changed)];

    /// <summary>
    /// Says that the server reads the changes to documents' files that its client says of (<c>workspace/didChangeWatchedFiles</c>), and says when each is read
    /// (<see cref="FileChangeRead"/>): a request about a document then waits for the changes to its file that arrived before it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The server's handler of the notification is marked <see cref="HandledOnArrivalAttribute"/>, and begins to read the files as it is handled. OmniSharp
    /// handles a notification it schedules after the requests before it, so a request that waits for the change would wait for itself.
    /// </para>
    /// <para>
    /// Every change the notification names is said to be read, a change to a file the server leaves alone included: one that is never said to be read is
    /// waited for until the request is cancelled. Until this is called, a change to a file is not waited for.
    /// </para>
    /// </remarks>
    public void ServeFileChanges() => _servesFileChanges = true;

    /// <summary>
    /// A change to a document's file has arrived from the client (<c>workspace/didChangeWatchedFiles</c>) and is not read yet: a request about the document
    /// waits for it (<see cref="WhenFileChangesReadAsync"/>), and each that runs, made of the document as it was, is answered <c>ContentModified</c>. A change
    /// to a document the server does not serve (<see cref="Serve(Func{Uri, bool})"/>) is nothing, and so is every change until the server serves file changes
    /// (<see cref="ServeFileChanges"/>).
    /// </summary>
    /// <param name="document">The document whose file changed.</param>
    public void FileChangeArrived(Uri document)
    {
        if (!_servesFileChanges || !_serves(document))
        {
            return;
        }

        var key = Key(document);
        CancellationTokenSource[] changed;
        lock (_gate)
        {
            if (!_files.TryGetValue(key, out var file))
            {
                _files[key] = file = new FileChanges();
            }

            file.Arrived++;
            changed = Watching(key);
        }

        foreach (var source in changed)
        {
            source.Cancel();
        }
    }

    /// <summary>
    /// A change to a document's file has been read: the document is what the file is, or the server leaves the file alone. The requests that wait for it go on.
    /// </summary>
    /// <param name="document">The document whose file changed.</param>
    public void FileChangeRead(Uri document)
    {
        var key = Key(document);
        TaskCompletionSource[] read;
        lock (_gate)
        {
            // a change that is not waited for, one that arrived before the server served file changes or to a document it does not serve, owes nothing.
            if (!_files.TryGetValue(key, out var file))
            {
                return;
            }

            file.Read++;
            read = [.. file.Waiting.Where(waiting => waiting.Until <= file.Read).Select(waiting => waiting.Read)];
            file.Waiting.RemoveAll(waiting => waiting.Until <= file.Read);
            if (file.Read >= file.Arrived)
            {
                _files.Remove(key);
            }
        }

        foreach (var source in read)
        {
            source.TrySetResult();
        }
    }

    /// <summary>
    /// A task that completes once every change to a document's file that has arrived (<see cref="FileChangeArrived"/>) is read
    /// (<see cref="FileChangeRead"/>). A change that arrives after the task is asked for is not waited for.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="token">Cancels the wait.</param>
    /// <returns>A task that completes once the changes are read, or is cancelled with <paramref name="token"/>.</returns>
    public Task WhenFileChangesReadAsync(Uri document, CancellationToken token) => WhenReadAsync(Key(document), token);

    /// <summary>
    /// A task that completes once every change to a file of any document that has arrived (<see cref="FileChangeArrived"/>) is read
    /// (<see cref="FileChangeRead"/>). A change that arrives after the task is asked for is not waited for.
    /// </summary>
    /// <param name="token">Cancels the wait.</param>
    /// <returns>A task that completes once the changes are read, or is cancelled with <paramref name="token"/>.</returns>
    public Task WhenAllFileChangesReadAsync(CancellationToken token) => WhenReadAsync(document: null, token);

    private Task WhenReadAsync(string? document, CancellationToken token)
    {
        List<Task> waits = [];
        lock (_gate)
        {
            foreach (var (key, file) in _files)
            {
                if (document is null || string.Equals(key, document, StringComparison.OrdinalIgnoreCase))
                {
                    var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    file.Waiting.Add((file.Arrived, read));
                    waits.Add(read.Task);
                }
            }
        }

        return waits.Count == 0 ? Task.CompletedTask : Task.WhenAll(waits).WaitAsync(token);
    }

    private bool FileChangesUnread(string? document)
        => document is null ? _files.Count > 0 : _files.ContainsKey(document);

    /// <summary>
    /// A change to a document has been applied: the server's handler of the notification has run.
    /// </summary>
    /// <param name="document">The document the change was to.</param>
    public void Applied(Uri document)
    {
        var key = Key(document);
        lock (_gate)
        {
            // a change the tap did not see, a message it could not read, is applied all the same: nothing is owed for it.
            if (_unapplied.GetValueOrDefault(key) is var count and > 1)
            {
                _unapplied[key] = count - 1;
            }
            else
            {
                _unapplied.Remove(key);
            }
        }
    }

    /// <summary>
    /// Answers a request about a document with what <paramref name="answer"/> makes of it, unless a change to the document arrives first: then the request
    /// stops, and is answered <c>ContentModified</c> (-32801). The answer is made once the changes to the document's file that arrived are read
    /// (<see cref="WhenFileChangesReadAsync"/>).
    /// </summary>
    /// <typeparam name="T">What the request answers.</typeparam>
    /// <param name="document">The document the request is about.</param>
    /// <param name="token">The request's own token, which the client cancels.</param>
    /// <param name="answer">Makes the answer, with a token that is cancelled when the client cancels the request or a change to the document arrives.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="RpcErrorException">A change to the document arrived before the answer was made: the error is <see cref="ErrorCodes.ContentModified"/>.</exception>
    public Task<T> WhileCurrentAsync<T>(Uri document, CancellationToken token, Func<CancellationToken, Task<T>> answer)
        => WatchAsync(Key(document), token, answer);

    /// <summary>
    /// Answers a request about every document of the workspace with what <paramref name="answer"/> makes of them, unless a change to any document arrives
    /// first: then the request stops, and is answered <c>ContentModified</c> (-32801). The answer is made once the changes to the documents' files that arrived
    /// are read (<see cref="WhenAllFileChangesReadAsync"/>).
    /// </summary>
    /// <remarks>
    /// A request such as <c>workspace/diagnostic</c> is made of every module; a keystroke in any of them would wait for all of them, and the completion the
    /// client sends after it too. The client asks again for the workspace as it is now.
    /// </remarks>
    /// <typeparam name="T">What the request answers.</typeparam>
    /// <param name="token">The request's own token, which the client cancels.</param>
    /// <param name="answer">Makes the answer, with a token that is cancelled when the client cancels the request or a change to any document arrives.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="RpcErrorException">A change to a document arrived before the answer was made: the error is <see cref="ErrorCodes.ContentModified"/>.</exception>
    public Task<T> WhileWorkspaceCurrentAsync<T>(CancellationToken token, Func<CancellationToken, Task<T>> answer)
        => WatchAsync(document: null, token, answer);

    private async Task<T> WatchAsync<T>(string? document, CancellationToken token, Func<CancellationToken, Task<T>> answer)
    {
        using var changed = new CancellationTokenSource();
        var watch = (document, changed);
        bool modified;

        // the changes to the files that arrived before the request are read first, and so are those that arrive while it waits for them.
        while (true)
        {
            await WhenReadAsync(document, token);
            lock (_gate)
            {
                if (FileChangesUnread(document))
                {
                    continue;
                }

                _watches.Add(watch);
                modified = document is null ? _unapplied.Count > 0 : _unapplied.ContainsKey(document);
                break;
            }
        }

        if (modified)
        {
            await changed.CancelAsync();
        }

        try
        {
            using var either = CancellationTokenSource.CreateLinkedTokenSource(token, changed.Token);
            either.Token.ThrowIfCancellationRequested();
            return await answer(either.Token).WaitAsync(either.Token);
        }
        catch (OperationCanceledException) when (changed.IsCancellationRequested && !token.IsCancellationRequested)
        {
            throw ContentModified();
        }
        finally
        {
            lock (_gate)
            {
                _watches.Remove(watch);
            }
        }
    }

    /// <summary>
    /// The error a request about a document is answered with when the document changed while the answer was made: <c>ContentModified</c> (-32801), which the
    /// client asks again after.
    /// </summary>
    /// <remarks>
    /// OmniSharp answers an <see cref="OperationCanceledException"/> <c>RequestCancelled</c>, its own <c>ContentModifiedException</c> included: an
    /// <see cref="RpcErrorException"/> is answered its code.
    /// </remarks>
    public static RpcErrorException ContentModified() => new(ErrorCodes.ContentModified, null!, ContentModifiedMessage);

    /// <summary>
    /// Wraps the stream the server reads its client's messages from, so that a change to a document is known as it arrives (<see cref="Arrived"/>). What is
    /// read is passed on unchanged.
    /// </summary>
    /// <param name="input">The stream the client's messages arrive on.</param>
    /// <returns>The stream to read them from.</returns>
    public Stream Tap(Stream input) => new DocumentChangeTap(input, this);

    // the same document, however its URI is spelled on the wire and in a handler: both go through OmniSharp's DocumentUri.
    private static string Key(Uri document) => document.AbsoluteUri;

    internal static Uri? DocumentOf(string uri)
    {
        try
        {
            return DocumentUri.From(uri).ToUri();
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
