using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Workspace;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// Request for <c>rdcore/diagnostics/projectModules</c>: the language server hands a diagnostics-provider extension every module of a project of the
/// workspace and asks for the diagnostics its project modules analyzers find (<see cref="IProjectModulesAnalyzer"/>).
/// </summary>
/// <remarks>
/// The payload rides a <see cref="System.Text.Json"/> string in <see cref="Json"/>, as <see cref="DiagnoseDocumentParams"/>'s does
/// (<see cref="PlatformJson"/>). Only extensions whose manifest advertises the <see cref="RDCore.SDK.Client.DiagnoseProjectModules"/> capability receive this
/// request.
/// </remarks>
[Method(RDCoreExtensionProtocol.DiagnoseProjectModules, Direction.ClientToServer)]
public record class DiagnoseProjectModulesParams : IRequest, IRequest<DiagnoseProjectModulesResult>
{
    /// <summary>
    /// The <see cref="System.Text.Json"/> representation of a <see cref="DiagnoseProjectModulesPayload"/>.
    /// </summary>
    public string Json { get; init; } = string.Empty;
}

/// <summary>
/// The <see cref="System.Text.Json"/> payload carried in <see cref="DiagnoseProjectModulesParams.Json"/>.
/// </summary>
/// <param name="ProjectFile">The project's <c>.rdproj</c>.</param>
/// <param name="Project">What the project file declares, as the language server read it.</param>
/// <param name="Modules">
/// Every module of the project, each as the language server sends it to a module analyzer (<see cref="DiagnoseDocumentPayload"/>): its document, the version
/// of its text, its parse, its semantics, its kind and the codes disabled for it.
/// </param>
/// <param name="Phase">
/// Which phase of the analysis the provider is asked for (<see cref="AnalysisPhase"/>): what it answers is the findings of that phase only. Every phase, when
/// not said.
/// </param>
public record class DiagnoseProjectModulesPayload(
    Uri ProjectFile, RDCoreProject Project, IReadOnlyList<DiagnoseDocumentPayload> Modules, AnalysisPhase Phase = AnalysisPhase.All)
{
    /// <summary>
    /// The codes whose diagnostics are disabled for the project: those of the platform's settings and of the workspace's together
    /// (<c>Analysis.DisabledDiagnostics</c>), which only the language server knows. The provider runs its analyzers with them:
    /// <see cref="ProjectModulesAnalyzerHost.Run(IEnumerable{IProjectModulesAnalyzer}, DiagnoseProjectModulesPayload, Microsoft.Extensions.Logging.ILogger)"/>
    /// drops what they report of a code that is disabled. None, unless said otherwise.
    /// </summary>
    public IReadOnlyList<RDCoreDiagnosticCode> DisabledCodes { get; init; } = [];
}

/// <summary>
/// Response to <c>rdcore/diagnostics/projectModules</c>: the diagnostics a provider found in the modules of the project, already projected to LSP
/// <see cref="Diagnostic"/>s, by the module each is in.
/// </summary>
public record class DiagnoseProjectModulesResult
{
    /// <summary>
    /// The diagnostics of each module that has some; a module of the payload that is not listed has none.
    /// </summary>
    public Container<ModuleDiagnostics> Modules { get; init; } = new();
}

/// <summary>
/// The diagnostics a provider found in one module of a project (<see cref="DiagnoseProjectModulesResult"/>).
/// </summary>
public record class ModuleDiagnostics
{
    /// <summary>
    /// The module the diagnostics are in: a <see cref="DiagnoseDocumentPayload.DocumentUri"/> of the payload.
    /// </summary>
    public Uri Document { get; init; } = default!;

    /// <summary>
    /// The <see cref="DiagnoseDocumentPayload.SourceVersion"/> of the module echoed back unchanged, so that the language server can drop the diagnostics of a
    /// module that changed while they were found.
    /// </summary>
    public int SourceVersion { get; init; }

    /// <summary>
    /// The diagnostics the provider found in the module.
    /// </summary>
    public Container<Diagnostic> Diagnostics { get; init; } = new();
}
