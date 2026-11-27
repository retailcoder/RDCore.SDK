using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace RDCore.SDK.Server.Auditing;

/// <summary>
/// The audit log: where the platform, and whatever runs on it, records the calls the execution policy decided.
/// </summary>
/// <remarks>
/// Recording never waits and never fails: an entry is written later, by another thread, and an entry that cannot be held or written is counted
/// (<see cref="Dropped"/>) and said in an entry of its own (<see cref="AuditEntryKind.Dropped"/>).
/// </remarks>
public interface IAuditLog
{
    /// <summary>
    /// Whether anything writes the entries: when it does not, a caller can leave an entry unmade.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// The entries dropped since the log was made: recorded while it was full, or not written.
    /// </summary>
    long Dropped { get; }

    /// <summary>
    /// Records an entry, to be written later. Never blocks and never throws.
    /// </summary>
    /// <param name="entry">The entry.</param>
    void Record(AuditEntry entry);
}

/// <summary>
/// The audit log that writes its entries to an <see cref="ILogger"/>, from a queue of its own and on a thread of its own, so that a call that records one
/// never waits for a file or a service.
/// </summary>
/// <remarks>
/// <para>
/// The queue holds <c>capacity</c> entries. An entry recorded while it is full is dropped and counted, and so is an entry the logger throws for. Before it
/// writes the next entry after a drop, and when it is disposed, the log writes an entry of <see cref="AuditEntryKind.Dropped"/> with the count since the last
/// one, so that the record says what it lacks.
/// </para>
/// <para>
/// Disposed, it takes no more entries and writes the ones it holds, for <c>shutdownTimeout</c> at most. An entry recorded after it is disposed is dropped and
/// counted.
/// </para>
/// </remarks>
public sealed class AuditLog : IAuditLog, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The key of an <see cref="ILoggerProvider"/> registered as a keyed service for the audit log: the provider is given every entry
    /// (<see cref="AuditServiceCollectionExtensions.AddAuditLog"/>).
    /// </summary>
    public const string ProviderKey = "audit";

    /// <summary>
    /// The category of the logger the entries are written to.
    /// </summary>
    public const string Category = "RDCore.Audit";

    /// <summary>
    /// The event every entry is written as.
    /// </summary>
    public static EventId Event { get; } = new(1, "Audit");

    /// <summary>
    /// The audit log that writes nothing, and takes no entry: what a component has when nothing is configured to write them.
    /// </summary>
    public static IAuditLog None { get; } = new NoAuditLog();

    private readonly Channel<AuditEntry> _queue;
    private readonly ILogger _writer;
    private readonly ILogger? _trace;
    private readonly TimeProvider _time;
    private readonly string? _component;
    private readonly int _process;
    private readonly TimeSpan _shutdownTimeout;
    private readonly Task _writing;
    private long _dropped;
    private long _unreported;
    private int _failureTraced;

    /// <summary>
    /// Makes the log, and starts writing what it is given.
    /// </summary>
    /// <param name="writer">The logger the entries are written to.</param>
    /// <param name="component">The component whose log it is, which an entry of dropped entries names.</param>
    /// <param name="process">The identifier of the component's process.</param>
    /// <param name="capacity">The entries the queue holds.</param>
    /// <param name="shutdownTimeout">How long the log goes on writing what it holds once it is disposed.</param>
    /// <param name="trace">Where the log says, once, that the logger failed; nowhere when <see langword="null"/>.</param>
    /// <param name="time">The clock of an entry of dropped entries; the system's when <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    public AuditLog(ILogger writer, string? component, int process, int capacity, TimeSpan shutdownTimeout, ILogger? trace = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _writer = writer;
        _component = component;
        _process = process;
        _shutdownTimeout = shutdownTimeout;
        _trace = trace;
        _time = time ?? TimeProvider.System;
        _queue = Channel.CreateBounded<AuditEntry>(new BoundedChannelOptions(capacity)
        {
            // TryWrite returns false when the queue is full, which is what counts a drop: nothing ever waits on it.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        _writing = Task.Run(WriteAllAsync);
    }

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <inheritdoc/>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <inheritdoc/>
    public void Record(AuditEntry entry)
    {
        if (entry is null || !_queue.Writer.TryWrite(entry))
        {
            Drop(1);
        }
    }

    private void Drop(long count)
    {
        Interlocked.Add(ref _dropped, count);
        Interlocked.Add(ref _unreported, count);
    }

    private async Task WriteAllAsync()
    {
        await foreach (var entry in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            WriteDropped();
            if (!TryWrite(entry))
            {
                Drop(1);
            }
        }

        WriteDropped();
    }

    private void WriteDropped()
    {
        var count = Interlocked.Exchange(ref _unreported, 0);
        if (count > 0 && !TryWrite(AuditEntry.OfDropped(_time.GetUtcNow(), _component, _process, count)))
        {
            // not written: the count is said with the next one.
            Interlocked.Add(ref _unreported, count);
        }
    }

    private bool TryWrite(AuditEntry entry)
    {
        try
        {
            _writer.Log(LogLevel.Information, Event, new AuditLogState(entry), null, static (state, _) => state.ToString());
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (_trace is { } trace && Interlocked.Exchange(ref _failureTraced, 1) == 0)
            {
                trace.LogError(exception, "📋 The audit log could not write an entry: it counts the entries it cannot write as dropped.");
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _queue.Writer.TryComplete();

        // the writing runs on the thread pool and never on the caller's context, so a caller that cannot await only waits for it, for the timeout at most.
#pragma warning disable VSTHRD002
        _ = _writing.Wait(_shutdownTimeout);
#pragma warning restore VSTHRD002
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        try
        {
            await _writing.WaitAsync(_shutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    private sealed class NoAuditLog : IAuditLog
    {
        public bool IsEnabled => false;

        public long Dropped => 0;

        public void Record(AuditEntry entry)
        {
        }
    }
}
