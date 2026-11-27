using RDCore.SDK.Server.Auditing;

namespace RDCore.SDK.Server.Configuration;

/// <summary>
/// The audit log of the platform, bound from the <c>Audit</c> section of its configuration: how many entries it holds before it drops them, what it records
/// of a call's arguments, and where its entries go (<see cref="IAuditLog"/>).
/// </summary>
public record class SdkAuditOptions
{
    /// <summary>
    /// The default of <see cref="Capacity"/>.
    /// </summary>
    public const int DefaultCapacity = 4096;

    /// <summary>
    /// The entries the log holds while they wait to be written: <see cref="DefaultCapacity"/> by default. An entry recorded while it is full is dropped and
    /// counted (<see cref="IAuditLog.Dropped"/>).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int Capacity { get; set; } = DefaultCapacity;

    /// <summary>
    /// How long the log goes on writing the entries it holds when the process ends, in seconds: <c>5</c> by default.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
    public int ShutdownTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// What an entry says of a call's arguments: <c>Types</c> by default, their number and declared types.
    /// </summary>
    public AuditArguments Arguments { get; set; } = AuditArguments.Types;

    /// <summary>
    /// The file the entries are written to, one line of JSON each (<see cref="AuditFileLoggerProvider"/>).
    /// </summary>
    public SdkAuditFileOptions File { get; set; } = new();

    /// <summary>
    /// The Windows Event Log the entries are written to, on Windows.
    /// </summary>
    public SdkAuditEventLogOptions EventLog { get; set; } = new();
}

/// <summary>
/// The file of the audit log (<see cref="SdkAuditOptions.File"/>).
/// </summary>
public record class SdkAuditFileOptions
{
    /// <summary>
    /// Whether the entries are written to a file: <c>true</c> by default.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The folder of the files: empty by default, for <see cref="Platform.IPlatformEnvironment.AuditsDirectory"/>. A relative path is relative to the platform
    /// root.
    /// </summary>
    public string Directory { get; set; } = string.Empty;
}

/// <summary>
/// The Windows Event Log of the audit log (<see cref="SdkAuditOptions.EventLog"/>).
/// </summary>
public record class SdkAuditEventLogOptions
{
    /// <summary>
    /// Whether the entries are written to the Windows Event Log: <c>false</c> by default. Ignored on another operating system.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The event log the entries are written to: <c>Application</c> by default.
    /// </summary>
    public string LogName { get; set; } = "Application";

    /// <summary>
    /// The source the entries are written as: <c>RDCore</c> by default.
    /// </summary>
    public string Source { get; set; } = "RDCore";
}
