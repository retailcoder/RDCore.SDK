using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Client;
using RDCore.SDK.Model.Diagnostics;
using RDCore.SDK.Platform;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// Answers <c>rdcore/diagnostics/document</c> for a diagnostics-provider extension (<see cref="DiagnoseDocument"/>): it runs the extension's analyzers on the
/// document the language server pushes down (<see cref="DiagnoseDocumentPayload"/>), and answers with their findings as diagnostics.
/// </summary>
/// <remarks>
/// An extension that reports analyzers' findings only registers this handler as it is, with its <see cref="IModuleAnalyzer"/>s and an
/// <see cref="ICoreDiagnosticsFactory"/>. An extension that reports diagnostics of its own besides them derives from it (<see cref="DiagnosticsOf"/>).
/// <list type="bullet">
/// <item>The handler runs on a thread whose stack has room for the deepest expression a parse has (<see cref="LargeStackThread"/>), since an analyzer may
/// walk an expression by recursion.</item>
/// <item>The analyzers are run with <see cref="ModuleAnalyzerHost"/>: those of the phase asked for, and of a code the document does not disable.</item>
/// <item>A finding's fixes are of the version of the document it was found in (<see cref="DiagnosticFixes.AtVersion"/>), so that the language server offers
/// them for that version only.</item>
/// <item>The result echoes <see cref="DiagnoseDocumentPayload.SourceVersion"/>, so that the language server can drop a report that raced a later
/// edit.</item>
/// </list>
/// </remarks>
/// <param name="diagnostics">Makes the diagnostics of the findings: their codes, their help links and their fixes.</param>
/// <param name="analyzers">The extension's analyzers.</param>
/// <param name="logger">The extension's logger: what the handler reports of each document, and the warnings of <see cref="ModuleAnalyzerHost"/>.</param>
[Method(RDCoreExtensionProtocol.DiagnoseDocument)]
public class AnalyzerDiagnosticsHandler(
    ICoreDiagnosticsFactory diagnostics, IEnumerable<IModuleAnalyzer> analyzers, ILogger<AnalyzerDiagnosticsHandler> logger)
    : RDCoreRequestHandler<DiagnoseDocumentParams, DiagnoseDocumentResult>
{
    /// <summary>
    /// Makes the diagnostics of the findings, and of what a derived handler reports of its own.
    /// </summary>
    protected ICoreDiagnosticsFactory Diagnostics => diagnostics;

    /// <inheritdoc/>
    protected sealed override Task<DiagnoseDocumentResult> HandleAsync(DiagnoseDocumentParams request, CancellationToken token)
        => Task.FromResult(LargeStackThread.Run(() => Diagnose(PlatformJson.Deserialize<DiagnoseDocumentPayload>(request.Json))));

    /// <summary>
    /// The diagnostics a derived handler reports of its own, besides the analyzers' findings, such as the syntax, compile and run-time errors the payload
    /// holds. They come first in the result. None, unless a derived handler says otherwise.
    /// </summary>
    /// <param name="payload">The document, its parse and its semantics, and the phase of the analysis asked for.</param>
    /// <returns>The diagnostics of the phase asked for (<see cref="DiagnoseDocumentPayload.Phase"/>).</returns>
    protected virtual IEnumerable<Diagnostic> DiagnosticsOf(DiagnoseDocumentPayload payload) => [];

    private DiagnoseDocumentResult Diagnose(DiagnoseDocumentPayload payload)
    {
        List<Diagnostic> found =
        [
            .. DiagnosticsOf(payload),
            .. ModuleAnalyzerHost.Run(analyzers, payload, logger)
                .Select(finding => diagnostics.FromAnalyzerFinding(finding with { Fixes = finding.Fixes?.AtVersion(payload.SourceVersion) })),
        ];

        logger.LogInformation("📥 {method}: {uri} → {count} diagnostic(s)", RDCoreExtensionProtocol.DiagnoseDocument, payload.DocumentUri, found.Count);
        return new DiagnoseDocumentResult { Diagnostics = found, SourceVersion = payload.SourceVersion };
    }
}
