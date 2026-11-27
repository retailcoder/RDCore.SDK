namespace RDCore.SDK.Runtime;

/// <summary>
/// What a call reaches outside the program: the category of a member, which the platform's execution policy decides by (<see cref="ExecutionPolicy"/>).
/// </summary>
/// <remarks>
/// Every call has exactly one category: a member's, or, for a member whose calls differ by their arguments (an <c>Open</c> for reading or for writing),
/// the call's own. A member that nobody marks is <see cref="Workspace"/>, the default value.
/// </remarks>
public enum ExecutionCategory
{
    /// <summary>
    /// The workspace's own code, and every member that reaches nothing outside the session (<c>Mid$</c>, <c>ChDir</c>, <c>MacScript</c>): never subject to
    /// the policy, never audited, and not in its settings.
    /// </summary>
    Workspace = 0,

    /// <summary>
    /// A call of a procedure a <c>Declare</c> statement imports from a native library.
    /// </summary>
    LibraryImports,

    /// <summary>
    /// The creation of an object of a library an automation server holds (<c>New Excel.Application</c>, <c>CreateObject</c>, <c>GetObject</c>), and any call on
    /// one. A library the platform implements itself is not automation.
    /// </summary>
    Automation,

    /// <summary>
    /// The start of another program: <c>Shell</c>.
    /// </summary>
    ProcessLaunch,

    /// <summary>
    /// What reaches the window of another application: <c>SendKeys</c>, <c>AppActivate</c>.
    /// </summary>
    Keystrokes,

    /// <summary>
    /// A change to the file system: <c>Kill</c>, <c>FileCopy</c>, <c>MkDir</c>, <c>RmDir</c>, <c>SetAttr</c>, <c>Name</c>, an <c>Open</c> for writing, and
    /// the members of the platform's libraries that do the same.
    /// </summary>
    FileSystemWrite,

    /// <summary>
    /// A read of the file system: <c>Dir</c>, <c>FileLen</c>, <c>FileDateTime</c>, <c>GetAttr</c>, an <c>Open</c> for <c>Input</c>, and the members of the
    /// platform's libraries that do the same.
    /// </summary>
    FileSystemRead,

    /// <summary>
    /// The Windows Registry: the settings members (<c>SaveSetting</c>, <c>GetSetting</c>, <c>GetAllSettings</c>, <c>DeleteSetting</c>) where
    /// <see cref="Abstract.Execution.IRuntimeEnvironmentProfile.LegacyRegistrySettings"/> puts them in it.
    /// </summary>
    Registry,

    /// <summary>
    /// The environment variables of the process: <c>Environ</c>, <c>Environ$</c>.
    /// </summary>
    Environment,
}

/// <summary>
/// What the execution policy does with a call of a category: make it, or refuse it.
/// </summary>
/// <remarks>
/// The modes are ordered from the least strict to the strictest. Where two rules decide one call, the stricter mode is the one that applies.
/// </remarks>
public enum ExecutionPolicyMode
{
    /// <summary>
    /// The call is made.
    /// </summary>
    Allow = 0,

    /// <summary>
    /// The call is refused with run-time error 70, <em>Permission denied</em>, which an error handler can trap.
    /// </summary>
    Block,

    /// <summary>
    /// The call is refused and the program stops: no error handler runs. A call known before the program runs is a compile error, and the program does
    /// not start.
    /// </summary>
    Halt,
}

/// <summary>
/// Which calls of a category the audit log records.
/// </summary>
public enum ExecutionAuditLevel
{
    /// <summary>
    /// None.
    /// </summary>
    None = 0,

    /// <summary>
    /// The calls the policy refuses.
    /// </summary>
    Refused,

    /// <summary>
    /// Every call, made or refused.
    /// </summary>
    All,
}

/// <summary>
/// What the execution policy does with the calls of one category: its mode, and what the audit log records of them.
/// </summary>
/// <param name="Mode">Whether the calls are made or refused.</param>
/// <param name="Audit">Which of the calls the audit log records.</param>
public readonly record struct ExecutionRule(ExecutionPolicyMode Mode = ExecutionPolicyMode.Allow, ExecutionAuditLevel Audit = ExecutionAuditLevel.None)
{
    /// <summary>
    /// The rule that makes every call and records none: the rule of a category the settings say nothing of, and of <see cref="ExecutionCategory.Workspace"/>.
    /// </summary>
    public static ExecutionRule PassThrough { get; } = new();

    /// <summary>
    /// Whether the rule makes every call and records none, so that a call it decides needs nothing done.
    /// </summary>
    public bool IsPassThrough => Mode == ExecutionPolicyMode.Allow && Audit == ExecutionAuditLevel.None;

    /// <summary>
    /// Whether the rule refuses the calls it decides.
    /// </summary>
    public bool Refuses => Mode != ExecutionPolicyMode.Allow;

    /// <summary>
    /// Whether the audit log records a call this rule decides.
    /// </summary>
    /// <param name="refused">Whether the call was refused.</param>
    public bool Records(bool refused) => Audit == ExecutionAuditLevel.All || (refused && Audit == ExecutionAuditLevel.Refused);

    /// <summary>
    /// This rule, with a mode at least as strict as <paramref name="mode"/>.
    /// </summary>
    /// <param name="mode">The least strict mode the rule may have.</param>
    public ExecutionRule AtLeast(ExecutionPolicyMode mode) => mode > Mode ? this with { Mode = mode } : this;
}

/// <summary>
/// The platform's execution policy: for each category of call that reaches outside the program, whether it is made, and whether the audit log records it.
/// </summary>
/// <remarks>
/// <para>
/// A session's policy never changes while it runs (<see cref="Abstract.Execution.IRuntimeEnvironmentProfile.ExecutionPolicy"/>), so what it decides of a member
/// can be decided once. Every rule is <see cref="ExecutionRule.PassThrough"/> unless the settings say otherwise
/// (<see cref="Server.Configuration.SdkEnvironmentOptions.ExecutionPolicy"/>).
/// </para>
/// <para>
/// <see cref="FileIO"/> is not a category: a mode other than <see cref="ExecutionPolicyMode.Allow"/> takes the file system out of the session, and every file
/// statement and member refuses with that mode. Where it and the rule of <see cref="ExecutionCategory.FileSystemRead"/> or
/// <see cref="ExecutionCategory.FileSystemWrite"/> both decide a call, the stricter mode applies.
/// </para>
/// </remarks>
public sealed record class ExecutionPolicy
{
    /// <summary>
    /// The policy that makes every call and records none.
    /// </summary>
    public static ExecutionPolicy Default { get; } = new();

    /// <summary>
    /// The rule of <see cref="ExecutionCategory.LibraryImports"/>.
    /// </summary>
    public ExecutionRule LibraryImports { get; init; }

    /// <summary>
    /// The rule of <see cref="ExecutionCategory.Automation"/>.
    /// </summary>
    public ExecutionRule Automation { get; init; }

    /// <summary>
    /// The rule of <see cref="ExecutionCategory.ProcessLaunch"/>.
    /// </summary>
    public ExecutionRule ProcessLaunch { get; init; }

    /// <summary>
    /// The rule of <see cref="ExecutionCategory.Keystrokes"/>.
    /// </summary>
    public ExecutionRule Keystrokes { get; init; }

    /// <summary>
    /// The rule of <see cref="ExecutionCategory.FileSystemWrite"/>.
    /// </summary>
    public ExecutionRule FileSystemWrite { get; init; }

    /// <summary>
    /// The rule of <see cref="ExecutionCategory.FileSystemRead"/>.
    /// </summary>
    public ExecutionRule FileSystemRead { get; init; }

    /// <summary>
    /// The rule of <see cref="ExecutionCategory.Registry"/>.
    /// </summary>
    public ExecutionRule Registry { get; init; }

    /// <summary>
    /// The rule of <see cref="ExecutionCategory.Environment"/>.
    /// </summary>
    public ExecutionRule Environment { get; init; }

    /// <summary>
    /// Whether the session has a file system: <see cref="ExecutionPolicyMode.Allow"/> gives it one, and any other mode none, every file statement and member
    /// refusing with that mode. Its audit level says which file operations the audit log records.
    /// </summary>
    public ExecutionRule FileIO { get; init; }

    /// <summary>
    /// The rule of a category: <see cref="ExecutionRule.PassThrough"/> for <see cref="ExecutionCategory.Workspace"/>.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="category"/> is not one of the categories.</exception>
    public ExecutionRule this[ExecutionCategory category] => category switch
    {
        ExecutionCategory.Workspace => ExecutionRule.PassThrough,
        ExecutionCategory.LibraryImports => LibraryImports,
        ExecutionCategory.Automation => Automation,
        ExecutionCategory.ProcessLaunch => ProcessLaunch,
        ExecutionCategory.Keystrokes => Keystrokes,
        ExecutionCategory.FileSystemWrite => FileSystemWrite,
        ExecutionCategory.FileSystemRead => FileSystemRead,
        ExecutionCategory.Registry => Registry,
        ExecutionCategory.Environment => Environment,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>
    /// Whether every rule, <see cref="FileIO"/>'s included, makes every call and records none: the policy then needs nothing done for any call.
    /// </summary>
    public bool IsPassThrough => LibraryImports.IsPassThrough && Automation.IsPassThrough && ProcessLaunch.IsPassThrough && Keystrokes.IsPassThrough
        && FileSystemWrite.IsPassThrough && FileSystemRead.IsPassThrough && Registry.IsPassThrough && Environment.IsPassThrough && FileIO.IsPassThrough;

    /// <summary>
    /// This policy, where a switch that refuses library imports or automation makes the rule of its category at least <see cref="ExecutionPolicyMode.Block"/>:
    /// what <see cref="Abstract.Execution.IRuntimeEnvironmentProfile.AllowDllImports"/> and
    /// <see cref="Abstract.Execution.IRuntimeEnvironmentProfile.AllowAutomation"/> say.
    /// </summary>
    /// <param name="allowDllImports">Whether a library import may be called.</param>
    /// <param name="allowAutomation">Whether automation is allowed.</param>
    public ExecutionPolicy Allowing(bool allowDllImports, bool allowAutomation) => (allowDllImports, allowAutomation) switch
    {
        (true, true) => this,
        _ => this with
        {
            LibraryImports = allowDllImports ? LibraryImports : LibraryImports.AtLeast(ExecutionPolicyMode.Block),
            Automation = allowAutomation ? Automation : Automation.AtLeast(ExecutionPolicyMode.Block),
        },
    };
}
