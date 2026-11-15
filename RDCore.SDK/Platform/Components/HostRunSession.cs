using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Client;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Platform.Components;

/// <summary>
/// Request for <c>rdcore/host/runSession/open</c>: opens a run session, in which the procedures of a project are run one request at a time and its
/// module-level state lasts from one to the next. What a test runner opens for a test module, and the AI assistant for the procedures it runs.
/// </summary>
/// <remarks>
/// A run session is composed from the project as the environment host has loaded it, apart from the runtime session of the shell
/// (<see cref="HostSessionStatusParams"/>): what one does to its variables, the other does not see. It lasts until it is closed
/// (<see cref="HostRunSessionCloseParams"/>), the project is reloaded (<see cref="RDCoreComponentProtocol.HostProjectReload"/>), or the host exits.
/// <para>
/// A project the host has not loaded is refused with <see cref="RDCoreComponentErrors.ProjectNotLoaded"/>.
/// </para>
/// <para>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </para>
/// </remarks>
[Method(RDCoreComponentProtocol.HostRunSessionOpen, Direction.ClientToServer)]
public record class HostRunSessionOpenParams : IRequest, IRequest<HostRunSessionOpenResult>
{
    /// <summary>
    /// The project file (<c>.rdproj</c>) of the project to run: a workspace can hold several.
    /// </summary>
    public Uri ProjectUri { get; init; } = default!;
}

/// <summary>
/// The answer to <see cref="HostRunSessionOpenParams"/>.
/// </summary>
/// <remarks>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </remarks>
public record class HostRunSessionOpenResult
{
    /// <summary>
    /// The run session's identifier: opaque text, unique for the host's lifetime and never reused.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}

/// <summary>
/// Request for <c>rdcore/host/runSession/execute</c>: runs one procedure of a run session's project, with the arguments of a data row and the fakes of the
/// run, unattended.
/// </summary>
/// <remarks>
/// <para>
/// The run is unattended (<see cref="Runtime.Abstract.Execution.IRuntimeSession.RunsUnattended"/>), and what it prints is in the answer's
/// <see cref="ExecuteResult.Output"/>. A dialog that no fake answers fails the run.
/// </para>
/// <para>
/// An unhandled run-time or internal error ends the program as <c>End</c> does: the session's module-level variables are reset and its files closed, and the
/// answer says so (<see cref="HostRunSessionExecuteResult.SessionReset"/>). Cancelling the request (<c>$/cancelRequest</c>) ends the program the same way,
/// and the answer is <see cref="ExecutionOutcome.Halted"/>.
/// </para>
/// <para>
/// One program runs on the host at a time. A request while the shell's session or another run session runs a program is refused with
/// <see cref="RDCoreComponentErrors.HostBusy"/>, and one for a session that is closed or was never opened with
/// <see cref="RDCoreComponentErrors.UnknownSession"/>.
/// </para>
/// <para>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </para>
/// </remarks>
[Method(RDCoreComponentProtocol.HostRunSessionExecute, Direction.ClientToServer)]
public record class HostRunSessionExecuteParams : IRequest, IRequest<HostRunSessionExecuteResult>
{
    /// <summary>
    /// The run session, as <see cref="HostRunSessionOpenResult.SessionId"/> named it.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// The programmatic name of the module of the project that declares <see cref="EntryPoint"/>.
    /// </summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>
    /// The name of the procedure to run.
    /// </summary>
    public string EntryPoint { get; init; } = string.Empty;

    /// <summary>
    /// The arguments, in the procedure's declared parameter order: a data row. Each is Let-coerced to its parameter's declared type
    /// (<strong>MS-VBAL §5.5.1.2</strong>). A count the procedure does not take, or a coercion that fails, is the run-time error the call would raise in
    /// the program, in the answer.
    /// </summary>
    public IReadOnlyList<RunValue> Arguments { get; init; } = [];

    /// <summary>
    /// The fakes the run starts with, which replace the session's: <see langword="null"/> keeps the session's table as the previous run left it, so a fake
    /// set up in a test's initialization lasts through the test.
    /// </summary>
    public FakesTable? Fakes { get; init; }
}

/// <summary>
/// The answer to <see cref="HostRunSessionExecuteParams"/>: the run's <see cref="ExecuteResult"/>, and what the fakes saw.
/// </summary>
/// <remarks>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </remarks>
public record class HostRunSessionExecuteResult : ExecuteResult
{
    /// <summary>
    /// The calls of the targets the fakes name, in the order they were made. Empty for a run with no fakes.
    /// </summary>
    public IReadOnlyList<FakeInvocationRecord> Invocations { get; init; } = [];

    /// <summary>
    /// Whether the run ended the program as <c>End</c> does (an <c>End</c> statement, an unhandled error, or the request being cancelled), and reset the
    /// session's module-level variables: a runner initializes the module again before it runs the next procedure that depends on them.
    /// </summary>
    public bool SessionReset { get; init; }
}

/// <summary>
/// Request for <c>rdcore/host/runSession/close</c>: closes a run session, and releases what it holds. A session that is closed or was never opened is
/// ignored, and the answer says so (<see cref="HostRunSessionCloseResult.Closed"/>).
/// </summary>
/// <remarks>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </remarks>
[Method(RDCoreComponentProtocol.HostRunSessionClose, Direction.ClientToServer)]
public record class HostRunSessionCloseParams : IRequest, IRequest<HostRunSessionCloseResult>
{
    /// <summary>
    /// The run session, as <see cref="HostRunSessionOpenResult.SessionId"/> named it.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}

/// <summary>
/// The answer to <see cref="HostRunSessionCloseParams"/>.
/// </summary>
/// <remarks>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </remarks>
public record class HostRunSessionCloseResult
{
    /// <summary>
    /// Whether the request closed an open run session: <see langword="false"/> for a session that was already closed, or was never opened.
    /// </summary>
    public bool Closed { get; init; }
}

/// <summary>
/// The JSON-RPC error codes the environment host answers a component request with, when it refuses the request rather than running it.
/// </summary>
/// <remarks>
/// They lie outside the ranges JSON-RPC and LSP reserve (<c>-32768</c> to <c>-32000</c>, and <c>-32899</c> to <c>-32800</c>). A failure of the program a
/// request runs is a run-time error in its answer, never one of these.
/// <para>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </para>
/// </remarks>
public static class RDCoreComponentErrors
{
    /// <summary>
    /// The run session a request names is closed, or was never opened: a project reload closes every run session.
    /// </summary>
    public const int UnknownSession = -31001;

    /// <summary>
    /// The project a run session is opened for is not one the host has loaded.
    /// </summary>
    public const int ProjectNotLoaded = -31002;

    /// <summary>
    /// A program is running on the host, in the shell's session or in a run session: one runs at a time.
    /// </summary>
    public const int HostBusy = -31003;
}
