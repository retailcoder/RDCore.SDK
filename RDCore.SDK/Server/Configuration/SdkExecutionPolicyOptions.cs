using RDCore.SDK.Runtime;

namespace RDCore.SDK.Server.Configuration;

/// <summary>
/// The execution policy of the platform, bound from the <c>Environment:ExecutionPolicy</c> section of its configuration (<see cref="ExecutionPolicy"/>): one rule
/// for each category of call that reaches outside the program, and one for the file system.
/// </summary>
/// <remarks>
/// Every rule makes every call and records none unless the configuration says otherwise.
/// </remarks>
public record class SdkExecutionPolicyOptions
{
    /// <summary>
    /// The rule of a call of a procedure a <c>Declare</c> statement imports (<see cref="ExecutionCategory.LibraryImports"/>).
    /// </summary>
    public SdkExecutionRuleOptions LibraryImports { get; set; } = new();

    /// <summary>
    /// The rule of automation (<see cref="ExecutionCategory.Automation"/>).
    /// </summary>
    public SdkExecutionRuleOptions Automation { get; set; } = new();

    /// <summary>
    /// The rule of the start of another program (<see cref="ExecutionCategory.ProcessLaunch"/>).
    /// </summary>
    public SdkExecutionRuleOptions ProcessLaunch { get; set; } = new();

    /// <summary>
    /// The rule of what reaches another application's window (<see cref="ExecutionCategory.Keystrokes"/>).
    /// </summary>
    public SdkExecutionRuleOptions Keystrokes { get; set; } = new();

    /// <summary>
    /// The rule of a change to the file system (<see cref="ExecutionCategory.FileSystemWrite"/>).
    /// </summary>
    public SdkExecutionRuleOptions FileSystemWrite { get; set; } = new();

    /// <summary>
    /// The rule of a read of the file system (<see cref="ExecutionCategory.FileSystemRead"/>).
    /// </summary>
    public SdkExecutionRuleOptions FileSystemRead { get; set; } = new();

    /// <summary>
    /// The rule of the Windows Registry (<see cref="ExecutionCategory.Registry"/>).
    /// </summary>
    public SdkExecutionRuleOptions Registry { get; set; } = new();

    /// <summary>
    /// The rule of the environment variables of the process (<see cref="ExecutionCategory.Environment"/>).
    /// </summary>
    public SdkExecutionRuleOptions Environment { get; set; } = new();

    /// <summary>
    /// Whether a session has a file system at all (<see cref="ExecutionPolicy.FileIO"/>).
    /// </summary>
    public SdkExecutionRuleOptions FileIO { get; set; } = new();

    /// <summary>
    /// The policy these options say.
    /// </summary>
    public ExecutionPolicy ToPolicy() => new()
    {
        LibraryImports = LibraryImports.ToRule(),
        Automation = Automation.ToRule(),
        ProcessLaunch = ProcessLaunch.ToRule(),
        Keystrokes = Keystrokes.ToRule(),
        FileSystemWrite = FileSystemWrite.ToRule(),
        FileSystemRead = FileSystemRead.ToRule(),
        Registry = Registry.ToRule(),
        Environment = Environment.ToRule(),
        FileIO = FileIO.ToRule(),
    };
}

/// <summary>
/// The rule of one category of call, bound from its section of the execution policy (<see cref="ExecutionRule"/>).
/// </summary>
public record class SdkExecutionRuleOptions
{
    /// <summary>
    /// Whether the calls are made: <c>Allow</c> (the default), <c>Block</c> (run-time error 70, which a handler can trap) or <c>Halt</c> (the program stops).
    /// </summary>
    public ExecutionPolicyMode Mode { get; set; }

    /// <summary>
    /// Which of the calls the audit log records: <c>None</c> (the default), <c>Refused</c> or <c>All</c>.
    /// </summary>
    public ExecutionAuditLevel Audit { get; set; }

    /// <summary>
    /// The rule these options say.
    /// </summary>
    public ExecutionRule ToRule() => new(Mode, Audit);
}
