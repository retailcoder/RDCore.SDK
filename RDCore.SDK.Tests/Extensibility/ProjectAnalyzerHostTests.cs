using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The project analyzers of an extension are run as its module analyzers are: only those of a code that is not disabled, each told which of its codes are. A
/// finding of a disabled code is dropped and the analyzer named in a warning; an analyzer that fails loses its own findings, and not the others'.
/// </summary>
[TestClass]
public sealed class ProjectAnalyzerHostTests
{
    private static readonly RDCoreDiagnosticCode Named = new("RDC", 1024);
    private static readonly RDCoreDiagnosticCode Other = new("XYZ", 42);
    private static readonly Uri ProjectFile = new("file:///c:/ws/.rdproj");
    private static readonly SourceRange NameRange = new(new SourcePosition(1, 11), new SourcePosition(1, 21));

    private readonly RecordingLogger _logger = new();

    // an analyzer that reports each of its codes it is told is enabled, or every one of them when it does not ask, at the project's name; how many times
    // it ran.
    private sealed class Analyzer(IReadOnlyList<RDCoreDiagnosticCode> codes, bool asks = true) : IProjectAnalyzer
    {
        public int Runs { get; private set; }

        public IReadOnlyList<RDCoreDiagnosticCode> Codes => codes;

        public IEnumerable<AnalyzerFinding> Analyze(ProjectAnalysisContext context)
        {
            Runs++;
            return [.. codes.Where(code => !asks || context.IsEnabled(code))
                .Select(code => new AnalyzerFinding(code, context.NameRange ?? SourceRange.Empty, DiagnosticSeverity.Information, context.Project.Name))];
        }
    }

    private sealed class Failing : IProjectAnalyzer
    {
        public IReadOnlyList<RDCoreDiagnosticCode> Codes => [Other];

        public IEnumerable<AnalyzerFinding> Analyze(ProjectAnalysisContext context) => throw new InvalidOperationException("no");
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }

    private static ProjectAnalysisContext Context(params RDCoreDiagnosticCode[] disabled)
        => new(ProjectFile, new RDCoreProject { Name = RDCoreProject.DefaultName }, NameRange) { DisabledCodes = disabled.ToHashSet() };

    [TestMethod]
    public void AnAnalyzer_IsGivenTheProject_AndItsFindingsAreOfTheProjectFile()
    {
        var findings = ProjectAnalyzerHost.Run([new Analyzer([Named])], Context(), _logger);

        Assert.HasCount(1, findings);
        Assert.AreEqual(NameRange, findings[0].Range);
        Assert.AreEqual("VBAProject", findings[0].Message);
        Assert.IsEmpty(_logger.Warnings);
    }

    [TestMethod]
    public void AnAnalyzerWhoseEveryCodeIsDisabled_IsNotRun()
    {
        var analyzer = new Analyzer([Named]);

        Assert.IsEmpty(ProjectAnalyzerHost.Run([analyzer], Context(Named), _logger));
        Assert.AreEqual(0, analyzer.Runs);
    }

    [TestMethod]
    public void AFindingOfADisabledCode_IsDropped_AndTheAnalyzerNamed()
    {
        var analyzer = new Analyzer([Named, Other], asks: false);

        var findings = ProjectAnalyzerHost.Run([analyzer], Context(Other), _logger);

        Assert.AreEqual(Named, findings.Single().Code);
        Assert.IsTrue(_logger.Warnings.Single().Contains("disabled", StringComparison.Ordinal), _logger.Warnings.Single());
    }

    [TestMethod]
    public void AnAnalyzerThatFails_LosesItsFindings_AndNotTheOthers()
    {
        var findings = ProjectAnalyzerHost.Run([new Failing(), new Analyzer([Named])], Context(), _logger);

        Assert.AreEqual(Named, findings.Single().Code);
        StringAssert.Contains(_logger.Warnings.Single(), nameof(Failing));
    }

    [TestMethod]
    public void ThePayload_RoundTrips_WithTheProjectItsNameRangeAndTheDisabledCodes()
    {
        var project = new RDCoreProject { Name = "Naval", Modules = [new RDCoreModule { RelativeUri = "Module1.bas" }] };
        var sent = new DiagnoseProjectPayload(ProjectFile, project, NameRange)
        {
            DisabledCodes = [Other],
        };

        var payload = PlatformJson.Deserialize<DiagnoseProjectPayload>(PlatformJson.Serialize(sent));

        Assert.AreEqual(ProjectFile, payload.ProjectFile);
        Assert.AreEqual("Naval", payload.Project.Name);
        Assert.AreEqual("Module1.bas", payload.Project.Modules.Single().RelativeUri);
        Assert.AreEqual(NameRange, payload.NameRange);
        CollectionAssert.AreEqual(new[] { Other }, payload.DisabledCodes.ToArray());

        var findings = ProjectAnalyzerHost.Run([new Analyzer([Named, Other])], payload, _logger);
        Assert.AreEqual(Named, findings.Single().Code);
    }

    [TestMethod]
    public void APayload_DisablesNothing_UnlessSaidOtherwise()
        => Assert.IsEmpty(new DiagnoseProjectPayload(ProjectFile, new RDCoreProject()).DisabledCodes);
}
