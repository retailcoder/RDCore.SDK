using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Client;
using RDCore.SDK.Model.Diagnostics;
using RDCore.SDK.Platform;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// Answers <c>rdcore/diagnostics/projectModules</c> for a diagnostics-provider extension (<see cref="DiagnoseProjectModules"/>): it runs the extension's
/// project modules analyzers on the modules of the project the language server pushes down (<see cref="DiagnoseProjectModulesPayload"/>), and answers with
/// their findings as diagnostics, by the module each is in.
/// </summary>
/// <remarks>
/// An extension that has project modules analyzers registers this handler as it is, with its <see cref="IProjectModulesAnalyzer"/>s and an
/// <see cref="ICoreDiagnosticsFactory"/>, and provides the <see cref="DiagnoseProjectModules"/> capability.
/// <list type="bullet">
/// <item>The handler runs on a thread whose stack has room for the deepest expression a parse has (<see cref="LargeStackThread"/>), since an analyzer may
/// walk an expression by recursion.</item>
/// <item>The analyzers are run with <see cref="ProjectModulesAnalyzerHost"/>: those of the phase asked for, and of a code the project does not disable.</item>
/// <item>A finding's fixes are of the version of the module it was found in (<see cref="DiagnosticFixes.AtVersion"/>), so that the language server offers
/// them for that version only, and the diagnostics of each module echo its <see cref="DiagnoseDocumentPayload.SourceVersion"/>.</item>
/// </list>
/// </remarks>
/// <param name="diagnostics">Makes the diagnostics of the findings: their codes, their help links and their fixes.</param>
/// <param name="analyzers">The extension's project modules analyzers.</param>
/// <param name="logger">
/// The extension's logger: what the handler reports of each project, and the warnings of <see cref="ProjectModulesAnalyzerHost"/>.
/// </param>
[Method(RDCoreExtensionProtocol.DiagnoseProjectModules)]
public sealed class ProjectModulesAnalyzerDiagnosticsHandler(
    ICoreDiagnosticsFactory diagnostics, IEnumerable<IProjectModulesAnalyzer> analyzers, ILogger<ProjectModulesAnalyzerDiagnosticsHandler> logger)
    : RDCoreRequestHandler<DiagnoseProjectModulesParams, DiagnoseProjectModulesResult>
{
    /// <inheritdoc/>
    protected override Task<DiagnoseProjectModulesResult> HandleAsync(DiagnoseProjectModulesParams request, CancellationToken token)
        => Task.FromResult(LargeStackThread.Run(() => Diagnose(PlatformJson.Deserialize<DiagnoseProjectModulesPayload>(request.Json))));

    private DiagnoseProjectModulesResult Diagnose(DiagnoseProjectModulesPayload payload)
    {
        // the host keeps only the findings of a module of the payload, so each has one.
        var versions = payload.Modules.DistinctBy(module => module.DocumentUri).ToDictionary(module => module.DocumentUri, module => module.SourceVersion);
        var found = ProjectModulesAnalyzerHost.Run(analyzers, payload, logger)
            .GroupBy(finding => finding.Document!)
            .Select(module => new ModuleDiagnostics
            {
                Document = module.Key,
                SourceVersion = versions[module.Key],
                Diagnostics = new(module.Select(finding =>
                    diagnostics.FromAnalyzerFinding(finding with { Fixes = finding.Fixes?.AtVersion(versions[module.Key]) }))),
            })
            .ToList();

        logger.LogInformation("📥 {method}: {uri} → {count} diagnostic(s) in {modules} module(s)", RDCoreExtensionProtocol.DiagnoseProjectModules,
            payload.ProjectFile, found.Sum(module => module.Diagnostics.Count()), found.Count);
        return new DiagnoseProjectModulesResult { Modules = new(found) };
    }
}
