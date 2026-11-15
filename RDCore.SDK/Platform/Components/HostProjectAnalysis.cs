using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Client;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Platform.Components;

/// <summary>
/// Request for <c>rdcore/host/project/analyze</c>: the language server asks the environment host to compose a project of the workspace other than the one it
/// runs, for analysis only (<see cref="ProjectAnalysis"/>).
/// </summary>
/// <remarks>
/// <para>
/// The project is composed from the project file at <see cref="WorkspaceRoot"/>, with the references it lists, as the runtime session is composed from the
/// project the host runs: a name binds in it as it would in a run. A root the host already composed for analysis is composed again, empty of what was defined
/// in it. The language server then defines the project's modules with <see cref="DefineSymbolsParams"/> under that root, and asks for their models with
/// <see cref="HostSemanticsParams.ModuleUri"/>.
/// </para>
/// <para>
/// The runtime session is never changed by it. A program never runs in a project composed for analysis, and the requests that run, debug or discard address
/// the runtime session only. The root of the project the host runs is not composed for analysis (<see cref="HostProjectAnalyzeResult.Composed"/>).
/// </para>
/// <para>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </para>
/// </remarks>
[Method(RDCoreComponentProtocol.HostProjectAnalyze, Direction.ClientToServer)]
public record class HostProjectAnalyzeParams : IRequest, IRequest<HostProjectAnalyzeResult>
{
    /// <summary>
    /// The root of the project: the folder its project file is in, or the folder the platform makes a project of when it has none.
    /// </summary>
    public Uri WorkspaceRoot { get; init; } = default!;
}

/// <summary>
/// What composing a project for analysis did.
/// </summary>
/// <remarks>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </remarks>
public record class HostProjectAnalyzeResult
{
    /// <summary>
    /// Whether the project was composed. It is not when the host has not composed its runtime session yet, or when the root is the one of the project the
    /// host runs.
    /// </summary>
    public bool Composed { get; init; }

    /// <summary>
    /// The number of modules the project lists; 0 when it was not composed.
    /// </summary>
    public int ModuleCount { get; init; }
}

/// <summary>
/// Request for <c>rdcore/host/project/release</c>: the language server asks the environment host to drop a project it composed for analysis
/// (<see cref="HostProjectAnalyzeParams"/>), with what was defined in it and the models of its modules.
/// </summary>
/// <remarks>
/// <para>
/// A root the host did not compose for analysis is left as it is, and the request is answered as for any other: releasing it is not an error. The runtime
/// session is never changed by it, the root of the project the host runs included.
/// </para>
/// <para>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </para>
/// </remarks>
[Method(RDCoreComponentProtocol.HostProjectRelease, Direction.ClientToServer)]
public record class HostProjectReleaseParams : IRequest, IRequest<HostProjectReleaseResult>
{
    /// <summary>
    /// The root of the project, as it was given to <see cref="HostProjectAnalyzeParams.WorkspaceRoot"/>.
    /// </summary>
    public Uri WorkspaceRoot { get; init; } = default!;
}

/// <summary>
/// What releasing a project did.
/// </summary>
/// <remarks>
/// Part of the component protocol (<see cref="RDCoreComponentProtocol"/>), outside the SDK's compatibility promise.
/// </remarks>
public record class HostProjectReleaseResult
{
    /// <summary>
    /// Whether a project composed for analysis was dropped: <see langword="false"/> when the host had none at that root.
    /// </summary>
    public bool Released { get; init; }
}
