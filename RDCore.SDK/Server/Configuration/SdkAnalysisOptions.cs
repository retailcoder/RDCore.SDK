using RDCore.SDK.Extensibility.Diagnostics;

namespace RDCore.SDK.Server.Configuration;

/// <summary>
/// Code analysis settings: what the analyzers of the platform and of its extensions report, under <c>Configuration:Analysis</c>.
/// </summary>
public record class SdkAnalysisOptions
{
    /// <summary>
    /// The codes whose diagnostics are not reported: <c>RDC01001, XYZ00042</c>. The analyzers that report them are not run, or skip their work
    /// (<see cref="ModuleAnalyzerHost"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A code of the platform's analyzers or of an extension's can be disabled. A code of the language's own errors cannot
    /// (<see cref="RDCoreDiagnosticCode.CanBeDisabled"/>): it is no value of the setting, and one written by hand is not read.
    /// </para>
    /// <para>
    /// A workspace has a list of its own too (<see cref="IWorkspaceSettings"/>): a code either list says is disabled is (<see cref="DisabledCodesWith"/>). A client
    /// adds a code to a list, or removes one from it, one at a time (<see cref="UserSettingsStore.Add"/>).
    /// </para>
    /// </remarks>
    [DiagnosticCodeList]
    public List<string> DisabledDiagnostics { get; set; } = [];

    /// <summary>
    /// The codes of <see cref="DisabledDiagnostics"/> that can be disabled, each once: what is written in the list and is no such code is not one.
    /// </summary>
    public IReadOnlySet<RDCoreDiagnosticCode> DisabledCodes => CodesIn(DisabledDiagnostics);

    /// <summary>
    /// The codes disabled for a document of a workspace: those of <see cref="DisabledDiagnostics"/> and those of the workspace's list together
    /// (<see cref="IWorkspaceSettings"/>). A code either one disables is disabled.
    /// </summary>
    /// <param name="workspace">The workspace's list of <c>Analysis.DisabledDiagnostics</c>.</param>
    public IReadOnlySet<RDCoreDiagnosticCode> DisabledCodesWith(IEnumerable<string> workspace) => CodesIn(DisabledDiagnostics.Concat(workspace));

    /// <summary>
    /// The codes of diagnostics that can be disabled in a list of texts, each once, without regard to case: what is no such code is not one.
    /// </summary>
    /// <param name="texts">The texts: <c>rdc01001</c>, <c>XYZ00042</c>.</param>
    public static IReadOnlySet<RDCoreDiagnosticCode> CodesIn(IEnumerable<string> texts)
        => texts.Select(DisablableCodeOf).OfType<RDCoreDiagnosticCode>().ToHashSet();

    /// <summary>
    /// The code of a diagnostic that can be disabled a text is, without regard to case; <see langword="null"/> when it is none.
    /// </summary>
    /// <param name="text">The text: <c>rdc01001</c>.</param>
    public static RDCoreDiagnosticCode? DisablableCodeOf(string? text)
        => RDCoreDiagnosticCode.TryParse(text?.Trim().ToUpperInvariant(), out var code) && code.CanBeDisabled ? code : null;
}

/// <summary>
/// Says a setting that is a list of texts is a list of codes of diagnostics that can be disabled (<see cref="SettingValueKind.DiagnosticCodeList"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DiagnosticCodeListAttribute : Attribute;
