using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The project modules analyzers of an extension are given every module of a project at once, and each finding says which module it is in: it is kept there,
/// unless the project or that module disables its code. A finding of no module of the project is dropped, and the analyzer named in a warning. The rules of the
/// module analyzers' host hold otherwise: the phase asked for, an analyzer whose every code is disabled not run, one that fails losing its findings alone.
/// </summary>
[TestClass]
public sealed class ProjectModulesAnalyzerHostTests
{
    private static readonly RDCoreDiagnosticCode Duplicate = new("XYZ", 5);
    private static readonly RDCoreDiagnosticCode Other = new("XYZ", 6);
    private static readonly Uri ProjectFile = new("file:///c:/ws/.rdproj");
    private static readonly Uri Orders = new("file:///c:/ws/Orders.cls");
    private static readonly Uri Invoices = new("file:///c:/ws/Invoices.cls");

    private readonly RecordingLogger _logger = new();

    // an analyzer that reports each of its codes in each document it is told to, at a line of the code's number; what it was given, the last time it ran.
    private sealed class Analyzer(IReadOnlyList<RDCoreDiagnosticCode> codes, IReadOnlyList<Uri?> reportIn, AnalysisPhase phase = AnalysisPhase.Static)
        : IProjectModulesAnalyzer
    {
        public ProjectModulesAnalysisContext? Given { get; private set; }

        public IReadOnlyList<RDCoreDiagnosticCode> Codes => codes;

        public AnalysisPhase Phase => phase;

        public IEnumerable<AnalyzerFinding> Analyze(ProjectModulesAnalysisContext context)
        {
            Given = context;
            return [.. reportIn.SelectMany(document => codes.Select(code =>
                new AnalyzerFinding(code, new SourceRange(code.Number, 0, code.Number, 1), DiagnosticSeverity.Warning, code.ToString())
                {
                    Document = document,
                }))];
        }
    }

    private sealed class Failing : IProjectModulesAnalyzer
    {
        public IReadOnlyList<RDCoreDiagnosticCode> Codes => [Other];

        public IEnumerable<AnalyzerFinding> Analyze(ProjectModulesAnalysisContext context) => throw new InvalidOperationException("no");
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

    private static ModuleAnalysisContext Module(Uri document, params RDCoreDiagnosticCode[] disabled)
        => new(document, new ModuleParseResult(), Semantics: null) { DisabledCodes = disabled.ToHashSet() };

    private static ProjectModulesAnalysisContext Project(IReadOnlyList<ModuleAnalysisContext> modules, params RDCoreDiagnosticCode[] disabled)
        => new(ProjectFile, new RDCoreProject { Name = "Billing" }, modules) { DisabledCodes = disabled.ToHashSet() };

    private static string[] Placed(IEnumerable<AnalyzerFinding> findings)
        => [.. findings.Select(finding => $"{finding.Code} in {finding.Document?.Segments[^1]}").Order()];

    [TestMethod]
    public void EachFinding_IsKept_InTheModuleItSays()
    {
        var analyzer = new Analyzer([Duplicate], [Orders, Invoices]);

        var findings = ProjectModulesAnalyzerHost.Run([analyzer], Project([Module(Orders), Module(Invoices)]), AnalysisPhase.All, _logger);

        CollectionAssert.AreEqual(new[] { "XYZ00005 in Invoices.cls", "XYZ00005 in Orders.cls" }, Placed(findings));
        Assert.IsEmpty(_logger.Warnings);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "a finding that says no module")]
    [DataRow("file:///c:/elsewhere/Orders.cls", DisplayName = "a finding of a document that is no module of the project")]
    public void AFindingOfNoModuleOfTheProject_IsDropped_AndTheAnalyzerNamedInAWarning(string? document)
    {
        var analyzer = new Analyzer([Duplicate], [document is null ? null : new Uri(document), Orders]);

        var findings = ProjectModulesAnalyzerHost.Run([analyzer], Project([Module(Orders)]), AnalysisPhase.All, _logger);

        CollectionAssert.AreEqual(new[] { "XYZ00005 in Orders.cls" }, Placed(findings));
        Assert.HasCount(1, _logger.Warnings);
        StringAssert.Contains(_logger.Warnings[0], nameof(Analyzer));
        StringAssert.Contains(_logger.Warnings[0], "XYZ00005");
    }

    [TestMethod]
    public void AFindingOfACodeItsModuleDisables_IsDropped_AndTheSameCodeInAnotherModuleIsKept()
    {
        var analyzer = new Analyzer([Duplicate], [Orders, Invoices]);

        var findings = ProjectModulesAnalyzerHost.Run([analyzer], Project([Module(Orders, Duplicate), Module(Invoices)]), AnalysisPhase.All, _logger);

        CollectionAssert.AreEqual(new[] { "XYZ00005 in Invoices.cls" }, Placed(findings));
        Assert.HasCount(1, _logger.Warnings);
        StringAssert.Contains(_logger.Warnings[0], Orders.ToString());
    }

    [TestMethod]
    public void AnAnalyzerWhoseEveryCodeTheProjectDisables_IsNotRun()
    {
        var disabled = new Analyzer([Duplicate], [Orders]);
        var enabled = new Analyzer([Other], [Orders]);

        var findings = ProjectModulesAnalyzerHost.Run([disabled, enabled], Project([Module(Orders)], Duplicate), AnalysisPhase.All, _logger);

        Assert.IsNull(disabled.Given);
        Assert.IsNotNull(enabled.Given);
        CollectionAssert.AreEqual(new[] { "XYZ00006 in Orders.cls" }, Placed(findings));
    }

    [TestMethod]
    public void TheAnalyzersOfAnotherPhase_AreNotRun()
    {
        var early = new Analyzer([Duplicate], [Orders]);
        var late = new Analyzer([Other], [Orders], AnalysisPhase.Runtime);

        var findings = ProjectModulesAnalyzerHost.Run([early, late], Project([Module(Orders)]), AnalysisPhase.Static, _logger);

        Assert.IsNull(late.Given);
        CollectionAssert.AreEqual(new[] { "XYZ00005 in Orders.cls" }, Placed(findings));
    }

    [TestMethod]
    public void AnAnalyzerThatFails_LosesItsFindingsAlone_AndIsNamedInAWarning()
    {
        var analyzer = new Analyzer([Duplicate], [Orders]);

        var findings = ProjectModulesAnalyzerHost.Run([new Failing(), analyzer], Project([Module(Orders)]), AnalysisPhase.All, _logger);

        CollectionAssert.AreEqual(new[] { "XYZ00005 in Orders.cls" }, Placed(findings));
        Assert.HasCount(1, _logger.Warnings);
        StringAssert.Contains(_logger.Warnings[0], nameof(Failing));
    }

    [TestMethod]
    public void TheModulesTheLanguageServerSends_AreGivenEachWithItsKindAndItsDisabledCodes()
    {
        var analyzer = new Analyzer([Duplicate], []);
        var payload = new DiagnoseProjectModulesPayload(ProjectFile, new RDCoreProject { Name = "Billing" },
        [
            new DiagnoseDocumentPayload(Orders, 3, new ModuleParseResult()) { ModuleKind = ModuleType.ClassModule, DisabledCodes = [Other] },
            new DiagnoseDocumentPayload(Invoices, 4, new ModuleParseResult()) { ModuleKind = ModuleType.DocumentModule },
        ])
        { DisabledCodes = [Other] };

        ProjectModulesAnalyzerHost.Run([analyzer], payload, _logger);

        var given = analyzer.Given!;
        Assert.AreEqual(ProjectFile, given.ProjectFile);
        Assert.AreEqual("Billing", given.Project.Name);
        Assert.AreEqual((ModuleType.ClassModule, true), (given.ModuleOf(Orders)!.ModuleKind, given.ModuleOf(Orders)!.DisabledCodes.Contains(Other)));
        Assert.AreEqual((ModuleType.DocumentModule, false), (given.ModuleOf(Invoices)!.ModuleKind, given.ModuleOf(Invoices)!.DisabledCodes.Contains(Other)));
        Assert.IsFalse(given.IsEnabled(Other));
        Assert.IsNull(given.ModuleOf(new Uri("file:///c:/ws/Customers.cls")));
    }
}
