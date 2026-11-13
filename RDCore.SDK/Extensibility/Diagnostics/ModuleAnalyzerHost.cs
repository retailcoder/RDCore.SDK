using Microsoft.Extensions.Logging;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// Runs the analyzers of an extension over a document, as the platform runs them for every extension: those of the phase asked for, and only those that report
/// a code that is not disabled for the document.
/// </summary>
/// <remarks>
/// <para>
/// An analyzer is run when the phase asked for includes its own (<see cref="IModuleAnalyzer.Phase"/>), and when any of its codes is enabled
/// (<see cref="IModuleAnalyzer.Codes"/>, <see cref="ModuleAnalysisContext.IsEnabled"/>): one whose every code is disabled is not run. An analyzer that declares
/// no code is always run.
/// </para>
/// <para>
/// An analyzer that is run is told which of its codes are disabled, and skips their work. A finding of a disabled code that it reports anyway is dropped, and
/// the analyzer is named in a warning: what is disabled is never reported, whatever an analyzer does. An analyzer that fails is named in a warning too, none of
/// its findings is kept, and the others' are.
/// </para>
/// </remarks>
public static class ModuleAnalyzerHost
{
    /// <summary>
    /// Runs the analyzers over the document of a <c>rdcore/diagnostics/document</c> request, in the phase it asks for, with the codes it says are disabled.
    /// </summary>
    /// <param name="analyzers">The analyzers of the extension.</param>
    /// <param name="payload">What the language server sent of the document.</param>
    /// <param name="logger">Where an analyzer that fails, or that reports a disabled code, is named.</param>
    /// <returns>The findings of every analyzer that was run, in the order of the analyzers.</returns>
    public static IReadOnlyList<AnalyzerFinding> Run(IEnumerable<IModuleAnalyzer> analyzers, DiagnoseDocumentPayload payload, ILogger logger)
        => Run(analyzers, new ModuleAnalysisContext(payload.DocumentUri, payload.ParseResult, payload.Semantics) { DisabledCodes = payload.DisabledCodes.ToHashSet() },
            payload.Phase, logger);

    /// <summary>
    /// Runs the analyzers of a phase over a module.
    /// </summary>
    /// <param name="analyzers">The analyzers of the extension.</param>
    /// <param name="context">The module, what is known of it, and the codes that are disabled for it.</param>
    /// <param name="phase">The phase asked for: an analyzer is run when it includes the analyzer's own.</param>
    /// <param name="logger">Where an analyzer that fails, or that reports a disabled code, is named.</param>
    /// <returns>The findings of every analyzer that was run, in the order of the analyzers.</returns>
    public static IReadOnlyList<AnalyzerFinding> Run(IEnumerable<IModuleAnalyzer> analyzers, ModuleAnalysisContext context, AnalysisPhase phase, ILogger logger)
    {
        var findings = new List<AnalyzerFinding>();
        foreach (var analyzer in analyzers.Where(analyzer => phase.HasFlag(analyzer.Phase) && ShouldRun(analyzer, context)))
        {
            List<AnalyzerFinding> found;
            try
            {
                found = [.. analyzer.Analyze(context)];
            }
            catch (Exception exception)
            {
                // one analyzer failing must not sink the others' findings.
                logger.LogWarning(exception, "❌ {analyzer} failed for {uri}.", analyzer.GetType().Name, context.Document);
                continue;
            }

            foreach (var finding in found)
            {
                if (context.IsEnabled(finding.Code))
                {
                    findings.Add(finding);
                }
                else
                {
                    logger.LogWarning("⚠️ {analyzer} reported {code}, which is disabled for {uri}: the finding is dropped.",
                        analyzer.GetType().Name, finding.Code, context.Document);
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// Whether an analyzer is run for a module: it declares no code, or any of its codes is enabled.
    /// </summary>
    /// <param name="analyzer">The analyzer.</param>
    /// <param name="context">The module, and the codes that are disabled for it.</param>
    public static bool ShouldRun(IModuleAnalyzer analyzer, ModuleAnalysisContext context)
        => analyzer.Codes.Count == 0 || analyzer.Codes.Any(context.IsEnabled);
}
