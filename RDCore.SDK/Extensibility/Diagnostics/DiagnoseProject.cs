using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Workspace;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// Request for <c>rdcore/diagnostics/project</c>: the language server hands a diagnostics-provider extension a project of the workspace and asks for the
/// diagnostics its project analyzers find (<see cref="IProjectAnalyzer"/>).
/// </summary>
/// <remarks>
/// The payload rides a <see cref="System.Text.Json"/> string in <see cref="Json"/>, as <see cref="DiagnoseDocumentParams"/>'s does
/// (<see cref="PlatformJson"/>). Only extensions whose manifest advertises the <see cref="RDCore.SDK.Client.DiagnoseProject"/> capability receive this
/// request.
/// </remarks>
[Method(RDCoreExtensionProtocol.DiagnoseProject, Direction.ClientToServer)]
public record class DiagnoseProjectParams : IRequest, IRequest<DiagnoseProjectResult>
{
    /// <summary>
    /// The <see cref="System.Text.Json"/> representation of a <see cref="DiagnoseProjectPayload"/>.
    /// </summary>
    public string Json { get; init; } = string.Empty;
}

/// <summary>
/// The <see cref="System.Text.Json"/> payload carried in <see cref="DiagnoseProjectParams.Json"/>.
/// </summary>
/// <param name="ProjectFile">The project's <c>.rdproj</c>, which the diagnostics are of.</param>
/// <param name="Project">What the project file declares, as the language server read it.</param>
/// <param name="NameRange">
/// Where the text of the project file writes the project's name (<see cref="RDCore.SDK.Workspace.ProjectFile.NameRangeIn"/>); <see langword="null"/> when it
/// does not.
/// </param>
public record class DiagnoseProjectPayload(Uri ProjectFile, RDCoreProject Project, SourceRange? NameRange = null)
{
    /// <summary>
    /// The codes whose diagnostics are disabled for the project: those of the platform's settings and of the workspace's together
    /// (<c>Analysis.DisabledDiagnostics</c>), which only the language server knows. The provider runs its analyzers with them
    /// (<see cref="ProjectAnalyzerHost.Run(IEnumerable{IProjectAnalyzer}, DiagnoseProjectPayload, Microsoft.Extensions.Logging.ILogger)"/>). None, unless said
    /// otherwise.
    /// </summary>
    public IReadOnlyList<RDCoreDiagnosticCode> DisabledCodes { get; init; } = [];
}

/// <summary>
/// Response to <c>rdcore/diagnostics/project</c>: the diagnostics a provider found of the project, already projected to LSP <see cref="Diagnostic"/>s, at
/// ranges of its project file.
/// </summary>
public record class DiagnoseProjectResult
{
    /// <summary>
    /// The diagnostics the provider found.
    /// </summary>
    public Container<Diagnostic> Diagnostics { get; init; } = new();
}
