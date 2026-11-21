using RDCore.SDK.Model.Source;
using RDCore.SDK.Workspace;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// What a project analyzer is given of a project: what its project file declares, and where the file writes the project's name.
/// </summary>
/// <remarks>
/// A finding of a project analyzer is of the project file (<see cref="ProjectFile"/>): its range is a range of that file, and the platform reports it there,
/// with what is wrong with the project file itself.
/// </remarks>
/// <param name="ProjectFile">The project file, the <c>.rdproj</c> of the project (<see cref="RDCore.SDK.Workspace.ProjectFile.FileName"/>).</param>
/// <param name="Project">What the project file declares: the project's name, its references and its modules.</param>
/// <param name="NameRange">
/// Where the text of the project file writes the project's name (<see cref="RDCore.SDK.Workspace.ProjectFile.NameRangeIn"/>); <see langword="null"/> when it
/// does not, and the project is named after its folder.
/// </param>
public sealed record class ProjectAnalysisContext(Uri ProjectFile, RDCoreProject Project, SourceRange? NameRange)
{
    /// <summary>
    /// The codes whose diagnostics are disabled for the project, in the platform's settings or the workspace's (<c>Analysis.DisabledDiagnostics</c>): an
    /// analyzer does not report them, and skips the work of finding them (<see cref="IsEnabled"/>). None, unless said otherwise.
    /// </summary>
    public IReadOnlySet<RDCoreDiagnosticCode> DisabledCodes { get; init; } = new HashSet<RDCoreDiagnosticCode>();

    /// <summary>
    /// Whether the diagnostics of a code are reported for the project: an analyzer that reports several codes asks of each before it does the work of finding
    /// its diagnostics. A code of the language's own errors is always reported (<see cref="RDCoreDiagnosticCode.CanBeDisabled"/>).
    /// </summary>
    /// <param name="code">The code.</param>
    public bool IsEnabled(RDCoreDiagnosticCode code) => !code.CanBeDisabled || !DisabledCodes.Contains(code);
}

/// <summary>
/// Looks at a project and says what is worth saying about it as a whole, rather than about one of its modules (<strong>RD-VBAL §2.6.4</strong>).
/// </summary>
/// <remarks>
/// Implement this in an extension to add a diagnostic of a project: the platform calls every project analyzer it knows of for each project of the workspace,
/// and reports the findings against the project's <c>.rdproj</c>. A project analyzer is a pure function of the <see cref="ProjectAnalysisContext"/>. The
/// analyzers of an extension are run with <see cref="ProjectAnalyzerHost"/>; an extension that has some provides the
/// <see cref="RDCore.SDK.Client.DiagnoseProject"/> capability.
/// </remarks>
public interface IProjectAnalyzer
{
    /// <summary>
    /// The codes of every diagnostic the analyzer reports: an analyzer whose every code is disabled for a project is not run for it.
    /// </summary>
    IReadOnlyList<RDCoreDiagnosticCode> Codes { get; }

    /// <summary>
    /// What the analyzer has to say about the project.
    /// </summary>
    /// <param name="context">The project, and where its project file writes its name.</param>
    /// <returns>Every finding, at ranges of the project file, in no particular order; none when there is nothing to say.</returns>
    IEnumerable<AnalyzerFinding> Analyze(ProjectAnalysisContext context);
}
