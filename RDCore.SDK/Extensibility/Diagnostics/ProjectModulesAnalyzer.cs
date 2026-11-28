using RDCore.SDK.Workspace;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// What a project modules analyzer is given of a project: what its project file declares, and every module of it at once, each as a module analyzer is given
/// it.
/// </summary>
/// <remarks>
/// A finding of a project modules analyzer is of one module of the project, and says which (<see cref="AnalyzerFinding.Document"/>): its range is a range of
/// that module, and the platform reports it there, with what the module's own analyzers find.
/// </remarks>
/// <param name="ProjectFile">The project file, the <c>.rdproj</c> of the project (<see cref="RDCore.SDK.Workspace.ProjectFile.FileName"/>).</param>
/// <param name="Project">What the project file declares: the project's name, its references and its modules.</param>
/// <param name="Modules">
/// Every module of the project the language server has, each with its document, its parse, what the environment host found out about it when it has, its kind,
/// and the codes that are disabled for it (<see cref="ModuleAnalysisContext"/>). Each block of a file of blocks is a module of its own, addressed by its own
/// URI.
/// </param>
public sealed record class ProjectModulesAnalysisContext(Uri ProjectFile, RDCoreProject Project, IReadOnlyList<ModuleAnalysisContext> Modules)
{
    private Dictionary<Uri, ModuleAnalysisContext>? _byDocument;

    /// <summary>
    /// The codes whose diagnostics are disabled for the project, in the platform's settings or the workspace's (<c>Analysis.DisabledDiagnostics</c>): an
    /// analyzer does not report them, and skips the work of finding them (<see cref="IsEnabled"/>). None, unless said otherwise. A module can disable more of
    /// them (<see cref="ModuleAnalysisContext.DisabledCodes"/>), and a finding of a code that its module disables is dropped.
    /// </summary>
    public IReadOnlySet<RDCoreDiagnosticCode> DisabledCodes { get; init; } = new HashSet<RDCoreDiagnosticCode>();

    /// <summary>
    /// Whether the diagnostics of a code are reported for the project: an analyzer that reports several codes asks of each before it does the work of finding
    /// its diagnostics. A code of the language's own errors is always reported (<see cref="RDCoreDiagnosticCode.CanBeDisabled"/>).
    /// </summary>
    /// <param name="code">The code.</param>
    public bool IsEnabled(RDCoreDiagnosticCode code) => !code.CanBeDisabled || !DisabledCodes.Contains(code);

    /// <summary>
    /// The module of the project that a document is: what a finding in it is checked against.
    /// </summary>
    /// <param name="document">The document: a module's <see cref="ModuleAnalysisContext.Document"/>.</param>
    /// <returns>The module; <see langword="null"/> when the document is none of the project's modules.</returns>
    public ModuleAnalysisContext? ModuleOf(Uri document)
    {
        _byDocument ??= Modules.DistinctBy(module => module.Document).ToDictionary(module => module.Document);
        return _byDocument.GetValueOrDefault(document);
    }
}

/// <summary>
/// Looks at every module of a project at once and says what is worth saying of one module for what another one is (<strong>RD-VBAL §2.6.4</strong>): two
/// modules that declare one thing that must be declared once, say.
/// </summary>
/// <remarks>
/// <para>
/// Implement this in an extension to add a diagnostic that no one module has all it needs to say. A module analyzer (<see cref="IModuleAnalyzer"/>) is given
/// one module, and a project analyzer (<see cref="IProjectAnalyzer"/>) the project file alone; a project modules analyzer is given every module of the project
/// (<see cref="ProjectModulesAnalysisContext"/>), and each of its findings says which module it is in (<see cref="AnalyzerFinding.Document"/>). A finding
/// that is about several modules is reported in one of them, and says where the others are (<see cref="AnalyzerFinding.Related"/>), or is reported in each.
/// </para>
/// <para>
/// The platform asks for the project again whenever any module of it changes, and what is found then replaces what was found before, in every module: a finding
/// that is gone is cleared from the module it was in, though that module did not change. An analyzer is a pure function of the
/// <see cref="ProjectModulesAnalysisContext"/>. The analyzers of an extension are run with <see cref="ProjectModulesAnalyzerHost"/>; an extension that has
/// some provides the <see cref="RDCore.SDK.Client.DiagnoseProjectModules"/> capability and answers its request with
/// <see cref="ProjectModulesAnalyzerDiagnosticsHandler"/>.
/// </para>
/// </remarks>
public interface IProjectModulesAnalyzer
{
    /// <summary>
    /// The codes of every diagnostic the analyzer reports: an analyzer whose every code is disabled for the project is not run for it.
    /// </summary>
    IReadOnlyList<RDCoreDiagnosticCode> Codes { get; }

    /// <summary>
    /// The phase in which the analyzer is asked: the earliest that has everything it reads (<see cref="AnalysisPhase"/>), as for a module analyzer
    /// (<see cref="IModuleAnalyzer.Phase"/>).
    /// </summary>
    AnalysisPhase Phase => AnalysisPhase.Static;

    /// <summary>
    /// What the analyzer has to say about the modules of the project.
    /// </summary>
    /// <param name="context">The project, and every module of it.</param>
    /// <returns>
    /// Every finding, each with the module it is in (<see cref="AnalyzerFinding.Document"/>), in no particular order; none when there is nothing to say.
    /// </returns>
    IEnumerable<AnalyzerFinding> Analyze(ProjectModulesAnalysisContext context);
}
