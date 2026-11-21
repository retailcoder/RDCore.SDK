using Microsoft.Extensions.Logging;

namespace RDCore.SDK.Extensibility.Diagnostics;

// what the hosts of module and project analyzers do the same way with each analyzer they run.
internal static class AnalyzerRun
{
    // an analyzer is run when it declares no code, or when any of its codes is enabled.
    internal static bool ShouldRun(IReadOnlyList<RDCoreDiagnosticCode> codes, Func<RDCoreDiagnosticCode, bool> isEnabled)
        => codes.Count == 0 || codes.Any(isEnabled);

    // runs one analyzer and adds what it found to the findings: a finding of a disabled code is dropped, and an analyzer that fails, or that reports a code
    // it does not declare or one that is disabled, is named in a warning.
    internal static void Collect(string analyzer, IReadOnlyList<RDCoreDiagnosticCode> codes, Func<IEnumerable<AnalyzerFinding>> analyze,
        Func<RDCoreDiagnosticCode, bool> isEnabled, Uri document, ILogger logger, List<AnalyzerFinding> findings)
    {
        List<AnalyzerFinding> found;
        try
        {
            found = [.. analyze()];
        }
        catch (Exception exception)
        {
            // one analyzer failing must not sink the others' findings.
            logger.LogWarning(exception, "❌ {analyzer} failed for {uri}.", analyzer, document);
            return;
        }

        foreach (var finding in found)
        {
            // a code the analyzer does not declare would not keep it running were the ones it declares all disabled: it is reported, and so is the analyzer.
            if (codes.Count > 0 && !codes.Contains(finding.Code))
            {
                logger.LogWarning("⚠️ {analyzer} reported {code}, which is not among the codes it declares.", analyzer, finding.Code);
            }

            if (isEnabled(finding.Code))
            {
                findings.Add(finding);
            }
            else
            {
                logger.LogWarning("⚠️ {analyzer} reported {code}, which is disabled for {uri}: the finding is dropped.", analyzer, finding.Code, document);
            }
        }
    }
}
