using Microsoft.Extensions.Logging;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// Runs the project modules analyzers of an extension over the modules of a project, as the platform runs them for every extension: those of the phase asked
/// for, and only those that report a code that is not disabled for the project.
/// </summary>
/// <remarks>
/// <para>
/// These are the rules of <see cref="ModuleAnalyzerHost"/>, for a finding that is in one module of several. An analyzer whose every code is disabled for the
/// project is not run, and one that declares no code always is. A finding of a code that the project or the module it is in disables is dropped, and the
/// analyzer named in a warning; so is a finding that is in none of the modules the analyzer was given, or that does not say which one it is in
/// (<see cref="AnalyzerFinding.Document"/>), for it has no range to be reported at. An analyzer that reports a code it does not declare is named in a warning,
/// and its finding kept. An analyzer that fails is named in a warning too, none of its findings is kept, and the others' are.
/// </para>
/// </remarks>
public static class ProjectModulesAnalyzerHost
{
    /// <summary>
    /// Runs the analyzers over the modules of a <c>rdcore/diagnostics/projectModules</c> request, in the phase it asks for, with the codes it says are disabled
    /// for the project and for each module.
    /// </summary>
    /// <param name="analyzers">The project modules analyzers of the extension.</param>
    /// <param name="payload">What the language server sent of the project and of its modules.</param>
    /// <param name="logger">Where an analyzer that fails, or that reports a disabled code or a finding of no module, is named.</param>
    /// <returns>The findings of every analyzer that was run, in the order of the analyzers, each with the module it is in.</returns>
    public static IReadOnlyList<AnalyzerFinding> Run(IEnumerable<IProjectModulesAnalyzer> analyzers, DiagnoseProjectModulesPayload payload, ILogger logger)
        => Run(analyzers, ContextOf(payload), payload.Phase, logger);

    /// <summary>
    /// Runs the analyzers of a phase over the modules of a project.
    /// </summary>
    /// <param name="analyzers">The project modules analyzers of the extension.</param>
    /// <param name="context">The project, its modules, and the codes that are disabled for each.</param>
    /// <param name="phase">The phase asked for: an analyzer is run when it includes the analyzer's own.</param>
    /// <param name="logger">Where an analyzer that fails, or that reports a disabled code or a finding of no module, is named.</param>
    /// <returns>The findings of every analyzer that was run, in the order of the analyzers, each with the module it is in.</returns>
    public static IReadOnlyList<AnalyzerFinding> Run(
        IEnumerable<IProjectModulesAnalyzer> analyzers, ProjectModulesAnalysisContext context, AnalysisPhase phase, ILogger logger)
    {
        var findings = new List<AnalyzerFinding>();
        foreach (var analyzer in analyzers.Where(analyzer => phase.HasFlag(analyzer.Phase) && ShouldRun(analyzer, context)))
        {
            AnalyzerRun.Collect(analyzer.GetType().Name, analyzer.Codes, () => analyzer.Analyze(context),
                finding => finding.Document is { } document && context.ModuleOf(document) is { } module
                    ? (module.Document, code => context.IsEnabled(code) && module.IsEnabled(code))
                    : null,
                context.ProjectFile, logger, findings);
        }

        return findings;
    }

    /// <summary>
    /// Whether an analyzer is run for a project: it declares no code, or any of its codes is enabled for the project.
    /// </summary>
    /// <param name="analyzer">The project modules analyzer.</param>
    /// <param name="context">The project, and the codes that are disabled for it.</param>
    public static bool ShouldRun(IProjectModulesAnalyzer analyzer, ProjectModulesAnalysisContext context)
        => AnalyzerRun.ShouldRun(analyzer.Codes, context.IsEnabled);

    // what the analyzers are given of the project the language server sent: each module as a module analyzer is given it.
    internal static ProjectModulesAnalysisContext ContextOf(DiagnoseProjectModulesPayload payload)
        => new(payload.ProjectFile, payload.Project, [.. payload.Modules.Select(ModuleAnalyzerHost.ContextOf)])
        {
            DisabledCodes = payload.DisabledCodes.ToHashSet(),
        };
}
