using System.Collections;
using System.Globalization;
using RDCore.SDK.Runtime;

namespace RDCore.SDK.Server.Auditing;

/// <summary>
/// What an entry of the audit log records.
/// </summary>
public enum AuditEntryKind
{
    /// <summary>
    /// A call the execution policy decided.
    /// </summary>
    Call = 0,

    /// <summary>
    /// Entries the log dropped because it was full, or could not write: <see cref="AuditEntry.Dropped"/> says how many since the last such entry.
    /// </summary>
    Dropped,
}

/// <summary>
/// What the execution policy did with a call an entry records.
/// </summary>
public enum AuditDecision
{
    /// <summary>
    /// The call was made.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// The call was refused with an error a handler can trap (<see cref="ExecutionPolicyMode.Block"/>).
    /// </summary>
    Blocked,

    /// <summary>
    /// The call was refused and the program stopped (<see cref="ExecutionPolicyMode.Halt"/>).
    /// </summary>
    Halted,
}

/// <summary>
/// What an entry says of a call's arguments (<see cref="Configuration.SdkAuditOptions.Arguments"/>).
/// </summary>
public enum AuditArguments
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None = 0,

    /// <summary>
    /// Their number and declared types (<c>String, Integer</c>), and no value.
    /// </summary>
    Types,

    /// <summary>
    /// Their values. A value can be a password: a connection string, an argument to a native library.
    /// </summary>
    Values,
}

/// <summary>
/// One entry of the audit log: a call the execution policy decided, and where it was made.
/// </summary>
/// <remarks>
/// Whoever records an entry fills what it knows; a member it does not know is <see langword="null"/>.
/// </remarks>
public sealed record class AuditEntry
{
    /// <summary>
    /// What the entry records.
    /// </summary>
    public AuditEntryKind Kind { get; init; }

    /// <summary>
    /// When the call was decided, in UTC.
    /// </summary>
    public DateTimeOffset Time { get; init; }

    /// <summary>
    /// The component of the platform the call was made in (<c>EnvironmentHost</c>).
    /// </summary>
    public string? Component { get; init; }

    /// <summary>
    /// The identifier of the process the call was made in.
    /// </summary>
    public int Process { get; init; }

    /// <summary>
    /// The session the call was made in: the environment host's session, or the request a web host answered.
    /// </summary>
    public string? Session { get; init; }

    /// <summary>
    /// The root folder of the workspace of the program.
    /// </summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// The project of the program.
    /// </summary>
    public string? Project { get; init; }

    /// <summary>
    /// The module the call is written in.
    /// </summary>
    public string? Module { get; init; }

    /// <summary>
    /// The procedure the call is written in.
    /// </summary>
    public string? Procedure { get; init; }

    /// <summary>
    /// The line the call is written on, from 1.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// What the call reaches outside the program.
    /// </summary>
    public ExecutionCategory Category { get; init; }

    /// <summary>
    /// What was called, without its arguments: <c>kernel32!GetTickCount</c>, <c>Excel.Application</c>, <c>VBA.Interaction.Shell</c>.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// What the execution policy did with the call.
    /// </summary>
    public AuditDecision Decision { get; init; }

    /// <summary>
    /// The mode of the rule that decided the call.
    /// </summary>
    public ExecutionPolicyMode Mode { get; init; }

    /// <summary>
    /// The call's arguments, as <see cref="Configuration.SdkAuditOptions.Arguments"/> says to write them; <see langword="null"/> when it says nothing of them.
    /// </summary>
    public string? Arguments { get; init; }

    /// <summary>
    /// The entries dropped since the last entry of <see cref="AuditEntryKind.Dropped"/>: <see langword="null"/> for any other entry.
    /// </summary>
    public long? Dropped { get; init; }

    /// <summary>
    /// The entry that says <paramref name="count"/> entries were dropped.
    /// </summary>
    /// <param name="time">When it is written.</param>
    /// <param name="component">The component that dropped them.</param>
    /// <param name="process">The identifier of its process.</param>
    /// <param name="count">How many were dropped.</param>
    public static AuditEntry OfDropped(DateTimeOffset time, string? component, int process, long count)
        => new() { Kind = AuditEntryKind.Dropped, Time = time, Component = component, Process = process, Dropped = count };

    /// <summary>
    /// The entry in a line: what a log that writes text writes.
    /// </summary>
    public override string ToString() => Kind == AuditEntryKind.Dropped
        ? string.Create(CultureInfo.InvariantCulture, $"{Time:O} {Component} ({Process}): {Dropped} audit entries dropped")
        : string.Create(CultureInfo.InvariantCulture,
            $"{Time:O} {Component} ({Process}) {Decision} {Category} {Target}({Arguments}) at {Module}.{Procedure}:{Line} (mode {Mode}, session {Session})");
}

/// <summary>
/// What the audit log hands its <see cref="Microsoft.Extensions.Logging.ILogger"/>: the entry, and its members as the structured values of the message, so that
/// a provider that writes structured values (the Windows Event Log, any other) writes each of them.
/// </summary>
/// <remarks>
/// A provider made for the audit log reads <see cref="Entry"/> (<see cref="AuditFileLoggerProvider"/>); any other provider reads the values, or the message
/// (<see cref="AuditEntry.ToString"/>).
/// </remarks>
/// <param name="entry">The entry.</param>
public sealed class AuditLogState(AuditEntry entry) : IReadOnlyList<KeyValuePair<string, object?>>
{
    private KeyValuePair<string, object?>[]? _values;

    /// <summary>
    /// The entry.
    /// </summary>
    public AuditEntry Entry { get; } = entry;

    private KeyValuePair<string, object?>[] Values => _values ??=
    [
        new(nameof(AuditEntry.Kind), Entry.Kind),
        new(nameof(AuditEntry.Time), Entry.Time),
        new(nameof(AuditEntry.Component), Entry.Component),
        new(nameof(AuditEntry.Process), Entry.Process),
        new(nameof(AuditEntry.Session), Entry.Session),
        new(nameof(AuditEntry.Workspace), Entry.Workspace),
        new(nameof(AuditEntry.Project), Entry.Project),
        new(nameof(AuditEntry.Module), Entry.Module),
        new(nameof(AuditEntry.Procedure), Entry.Procedure),
        new(nameof(AuditEntry.Line), Entry.Line),
        new(nameof(AuditEntry.Category), Entry.Category),
        new(nameof(AuditEntry.Target), Entry.Target),
        new(nameof(AuditEntry.Decision), Entry.Decision),
        new(nameof(AuditEntry.Mode), Entry.Mode),
        new(nameof(AuditEntry.Arguments), Entry.Arguments),
        new(nameof(AuditEntry.Dropped), Entry.Dropped),
    ];

    /// <inheritdoc/>
    public KeyValuePair<string, object?> this[int index] => Values[index];

    /// <inheritdoc/>
    public int Count => Values.Length;

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)Values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public override string ToString() => Entry.ToString();
}
