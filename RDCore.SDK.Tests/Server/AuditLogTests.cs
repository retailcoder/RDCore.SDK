using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RDCore.SDK.Runtime;
using RDCore.SDK.Server.Auditing;

namespace RDCore.Tests.Server;

/// <summary>
/// The audit log writes what it is given on a thread of its own. What it cannot hold or write is counted, and said in an entry of its own before the next one
/// it writes, so that the record says what it lacks.
/// </summary>
[TestClass]
public sealed class AuditLogTests
{
    private static readonly DateTimeOffset ClockTime = ManualClock.Start;

    private static AuditEntry Call(string target) => new()
    {
        Time = ClockTime,
        Component = "EnvironmentHost",
        Process = 42,
        Category = ExecutionCategory.LibraryImports,
        Target = target,
        Decision = AuditDecision.Blocked,
        Mode = ExecutionPolicyMode.Block,
    };

    private static AuditLog Log(ILogger writer, int capacity = 16, ILogger? trace = null)
        => new(writer, "EnvironmentHost", 42, capacity, Timeout.InfiniteTimeSpan, trace, new ManualClock());

    [TestMethod]
    public void WhatIsRecorded_IsWritten_InOrder_ByTheTimeTheLogIsDisposed()
    {
        var writer = new RecordingLogger();
        var log = Log(writer);

        log.Record(Call("kernel32!GetTickCount"));
        log.Record(Call("user32!MessageBoxW"));
        log.Dispose();

        CollectionAssert.AreEqual(new[] { "kernel32!GetTickCount", "user32!MessageBoxW" }, writer.Entries.Select(entry => entry.Target).ToArray());
        Assert.AreEqual(0, log.Dropped);
    }

    [TestMethod]
    public void AnEntryRecordedWhileTheLogIsFull_IsDropped_AndSaidBeforeTheNextEntryWritten()
    {
        var writer = new RecordingLogger { Gated = true };
        var log = Log(writer, capacity: 1);

        log.Record(Call("first"));
        writer.Entered.Wait();          // the first is being written, and the queue is empty
        log.Record(Call("second"));     // the queue holds it
        log.Record(Call("third"));      // full: dropped
        log.Record(Call("fourth"));     // full: dropped
        writer.Release();
        log.Dispose();

        var entries = writer.Entries;
        Assert.HasCount(3, entries);
        Assert.AreEqual("first", entries[0].Target);
        Assert.AreEqual(AuditEntryKind.Dropped, entries[1].Kind);
        Assert.AreEqual(2, entries[1].Dropped);
        Assert.AreEqual(ClockTime, entries[1].Time);
        Assert.AreEqual("EnvironmentHost", entries[1].Component);
        Assert.AreEqual(42, entries[1].Process);
        Assert.AreEqual("second", entries[2].Target);
        Assert.AreEqual(2, log.Dropped);
    }

    [TestMethod]
    public void AnEntryTheLoggerThrowsFor_IsCounted_AndTheFailureIsTracedOnce()
    {
        var trace = new RecordingLogger();
        var log = Log(new ThrowingLogger(), trace: trace);

        log.Record(Call("first"));
        log.Record(Call("second"));
        log.Dispose();

        Assert.AreEqual(2, log.Dropped);
        Assert.AreEqual(1, trace.Errors);
    }

    [TestMethod]
    public void AnEntryRecordedOnceTheLogIsDisposed_IsDropped()
    {
        var writer = new RecordingLogger();
        var log = Log(writer);
        log.Dispose();

        log.Record(Call("late"));

        Assert.AreEqual(1, log.Dropped);
        Assert.IsEmpty(writer.Entries);
    }

    [TestMethod]
    public void TheLogOfNothing_WritesNothing_AndDropsNothing()
    {
        AuditLog.None.Record(Call("anything"));

        Assert.IsFalse(AuditLog.None.IsEnabled);
        Assert.AreEqual(0, AuditLog.None.Dropped);
    }

    [TestMethod]
    public void AnEntry_IsWrittenWithItsMembersAsStructuredValues()
    {
        var writer = new RecordingLogger();
        var log = Log(writer);

        log.Record(Call("kernel32!GetTickCount") with { Line = 7 });
        log.Dispose();

        var values = writer.States.Single().ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.AreEqual("kernel32!GetTickCount", values[nameof(AuditEntry.Target)]);
        Assert.AreEqual(ExecutionCategory.LibraryImports, values[nameof(AuditEntry.Category)]);
        Assert.AreEqual(7, values[nameof(AuditEntry.Line)]);
        Assert.AreEqual(AuditLog.Event, writer.Events.Single());
    }

    // records what the audit log hands it; gated, it holds the first entry until it is released.
    private sealed class RecordingLogger : ILogger
    {
        private readonly ConcurrentQueue<AuditLogState> _states = new();
        private readonly ConcurrentQueue<EventId> _events = new();
        private readonly ManualResetEventSlim _released = new();
        private int _errors;

        public bool Gated { get; init; }

        public ManualResetEventSlim Entered { get; } = new();

        public AuditLogState[] States => [.. _states];

        public AuditEntry[] Entries => [.. _states.Select(state => state.Entry)];

        public EventId[] Events => [.. _events];

        public int Errors => Volatile.Read(ref _errors);

        public void Release() => _released.Set();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Interlocked.Increment(ref _errors);
            }

            if (state is not AuditLogState audit)
            {
                return;
            }

            if (Gated && !Entered.IsSet)
            {
                Entered.Set();
                _released.Wait();
            }

            _states.Enqueue(audit);
            _events.Enqueue(eventId);
        }
    }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => throw new IOException("the disk is full");
    }
}
