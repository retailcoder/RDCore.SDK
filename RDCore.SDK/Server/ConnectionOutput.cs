using System.IO.Pipelines;

namespace RDCore.SDK.Server;

/// <summary>
/// The writer either end of a JSON-RPC connection writes its messages to the other with, which can be completed while a message is being written: the
/// writer it wraps, write for write, until it is completed, and then nothing more. A language server and its client both give it to OmniSharp as their
/// output (<c>WithOutput</c>).
/// </summary>
/// <remarks>
/// <para>
/// OmniSharp stops its output as its input ends, when the other end exits or closes the connection, and as a server handles <c>exit</c>: it completes its
/// writer, then cancels the loop that writes to it, as it is stopped and again as it is disposed. A message still being written then, as when the other end
/// has stopped reading, is cancelled through the token it was written with, and a stream's writer cancels the write with a token source it disposed as it
/// was completed: the <see cref="ObjectDisposedException"/> ends the process, or is thrown by the <c>Dispose</c> of the server or client.
/// </para>
/// <para>
/// This writer writes to the writer it wraps without the caller's token, and cancels the write itself only while the writer it wraps is not completed.
/// Completed while a message is being written, it cancels that write, and completes the writer it wraps without writing the rest: the other end of a
/// connection that is stopped reads nothing more of it.
/// </para>
/// <para>
/// Once it is completed, a write or a flush writes nothing and says the writing is over (<see cref="FlushResult.IsCompleted"/>), and a buffer it is asked
/// for is one of its own, which nothing reads: what is written after the end is dropped, and the writer it wraps is never written to as it is completed.
/// OmniSharp writes a message's header, then its body: an end that stops between the two leaves the other a header with no body, at the end of a stream
/// that ends there, which the other end reads as no message.
/// </para>
/// </remarks>
/// <param name="inner">The writer of the stream the messages are written to.</param>
public sealed class ConnectionOutput(PipeWriter inner) : PipeWriter
{
    private static readonly FlushResult Ended = new(isCanceled: false, isCompleted: true);

    private readonly object _gate = new();
    private Task _writing = Task.CompletedTask;
    private Task? _completion;

    /// <summary>
    /// Creates the writer of one end's messages to the stream it reads the other end's messages from as well.
    /// </summary>
    /// <remarks>
    /// The stream is left open as the writer is completed: OmniSharp completes its output before its input as a server handles <c>exit</c>, and a stream
    /// disposed under the read that waits on it fails that read. Whoever opened the stream disposes it.
    /// </remarks>
    /// <param name="stream">The stream the messages are written to, and the other end's messages are read from.</param>
    /// <returns>The output of that end of the connection.</returns>
    public static ConnectionOutput Over(Stream stream) => new(Create(stream, new StreamPipeWriterOptions(leaveOpen: true)));

    /// <inheritdoc/>
    public override void Advance(int bytes)
    {
        lock (_gate)
        {
            if (_completion is null)
            {
                inner.Advance(bytes);
            }
        }
    }

    /// <inheritdoc/>
    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        lock (_gate)
        {
            return _completion is null ? inner.GetMemory(sizeHint) : Discarded(sizeHint);
        }
    }

    /// <inheritdoc/>
    public override Span<byte> GetSpan(int sizeHint = 0)
    {
        lock (_gate)
        {
            return _completion is null ? inner.GetSpan(sizeHint) : Discarded(sizeHint);
        }
    }

    /// <inheritdoc/>
    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        => WriteAsync(() => inner.FlushAsync(CancellationToken.None), cancellationToken);

    /// <inheritdoc/>
    public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        => WriteAsync(() => inner.WriteAsync(source, CancellationToken.None), cancellationToken);

    /// <inheritdoc/>
    public override void CancelPendingFlush()
    {
        lock (_gate)
        {
            if (_completion is null)
            {
                inner.CancelPendingFlush();
            }
        }
    }

    /// <inheritdoc/>
    public override void Complete(Exception? exception = null) => _ = CompleteOnceAsync(exception);

    /// <inheritdoc/>
    public override ValueTask CompleteAsync(Exception? exception = null) => new(CompleteOnceAsync(exception));

    // a buffer of its own, asked for once the writer is completed: what is written to it is never written to the stream.
    private static byte[] Discarded(int sizeHint) => new byte[Math.Max(sizeHint, 1)];

    private async ValueTask<FlushResult> WriteAsync(Func<ValueTask<FlushResult>> write, CancellationToken cancellationToken)
    {
        TaskCompletionSource written;
        lock (_gate)
        {
            if (_completion is not null)
            {
                return Ended;
            }

            // the completion waits for this write before it completes the writer it wraps: the two never run at once.
            written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _writing = written.Task;
        }

        FlushResult flushed;
        try
        {
            using (cancellationToken.Register(CancelPendingFlush))
            {
                flushed = await write();
            }
        }
        finally
        {
            written.SetResult();
        }

        if (flushed.IsCanceled && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        return flushed;
    }

    // completed once, however many times it is told to.
    private Task CompleteOnceAsync(Exception? exception)
    {
        TaskCompletionSource completion;
        Task writing;
        lock (_gate)
        {
            if (_completion is { } started)
            {
                return started;
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion.Task;
            writing = _writing;
            if (!writing.IsCompleted)
            {
                inner.CancelPendingFlush();
            }
        }

        _ = CompleteAfterWritingAsync(writing, exception, completion);
        return completion.Task;
    }

    private async Task CompleteAfterWritingAsync(Task writing, Exception? exception, TaskCompletionSource completion)
    {
        try
        {
            if (writing.IsCompleted)
            {
                await inner.CompleteAsync(exception);
            }
            else
            {
                // the writer it wraps writes what it holds as it is completed, unless it is completed with a fault: what was not written is not written.
                await writing;
                await inner.CompleteAsync(exception ?? new OperationCanceledException("The output was completed while a message was being written."));
            }

            completion.TrySetResult();
        }
        catch (Exception failure)
        {
            completion.TrySetException(failure);
        }
    }
}
