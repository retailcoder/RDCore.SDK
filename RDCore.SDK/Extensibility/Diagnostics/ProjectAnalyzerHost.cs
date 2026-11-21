using Microsoft.Extensions.Logging;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// Runs the project analyzers of an extension over a project, as the platform runs them for every extension: only those that report a code that is not
/// disabled for the project.
/// </summary>
/// <remarks>
/// An analyzer whose every code is disabled is not run, and one that declares no code always is. A finding of a disabled code is dropped, and the analyzer
/// named in a warning; so is an analyzer that reports a code it does not declare, whose finding is kept. An analyzer that fails is named in a warning too,
/// none of its findings is kept, and the others' are. These are the rules of <see cref="ModuleAnalyzerHost"/>.
/// </remarks>
public static class ProjectAnalyzerHost
{
    /// <summary>
    /// Runs the analyzers over the project of a <c>rdcore/diagnostics/project</c> request, with the codes it says are disabled.
    /// </summary>
    /// <param name="analyzers">The project analyzers of the extension.</param>
    /// <param name="payload">What the language server sent of the project.</param>
    /// <param name="logger">Where an analyzer that fails, or that reports a disabled code, is named.</param>
    /// <returns>The findings of every analyzer that was run, in the order of the analyzers.</returns>
    public static IReadOnlyList<AnalyzerFinding> Run(IEnumerable<IProjectAnalyzer> analyzers, DiagnoseProjectPayload payload, ILogger logger)
        => Run(
            analyzers,
            new ProjectAnalysisContext(payload.ProjectFile, payload.Project, payload.NameRange) { DisabledCodes = payload.DisabledCodes.ToHashSet() },
            logger);

    /// <summary>
    /// Runs the analyzers over a project.
    /// </summary>
    /// <param name="analyzers">The project analyzers of the extension.</param>
    /// <param name="context">The project, and the codes that are disabled for it.</param>
    /// <param name="logger">Where an analyzer that fails, or that reports a disabled code, is named.</param>
    /// <returns>The findings of every analyzer that was run, in the order of the analyzers.</returns>
    public static IReadOnlyList<AnalyzerFinding> Run(IEnumerable<IProjectAnalyzer> analyzers, ProjectAnalysisContext context, ILogger logger)
    {
        var findings = new List<AnalyzerFinding>();
        foreach (var analyzer in analyzers.Where(analyzer => ShouldRun(analyzer, context)))
        {
            AnalyzerRun.Collect(
                analyzer.GetType().Name, analyzer.Codes, () => analyzer.Analyze(context), context.IsEnabled, context.ProjectFile, logger, findings);
        }

        return findings;
    }

    /// <summary>
    /// Whether an analyzer is run for a project: it declares no code, or any of its codes is enabled.
    /// </summary>
    /// <param name="analyzer">The project analyzer.</param>
    /// <param name="context">The project, and the codes that are disabled for it.</param>
    public static bool ShouldRun(IProjectAnalyzer analyzer, ProjectAnalysisContext context) => AnalyzerRun.ShouldRun(analyzer.Codes, context.IsEnabled);
}
