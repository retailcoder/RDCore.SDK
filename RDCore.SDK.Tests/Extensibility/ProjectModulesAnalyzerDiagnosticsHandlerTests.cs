using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.Diagnostics;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The handler an extension registers for <c>rdcore/diagnostics/projectModules</c> (<see cref="ProjectModulesAnalyzerDiagnosticsHandler"/>): it runs the
/// extension's project modules analyzers on the modules the language server pushes down, and answers with their findings as diagnostics, under the module each
/// is in and of that module's version.
/// </summary>
[TestClass]
public sealed class ProjectModulesAnalyzerDiagnosticsHandlerTests
{
    private static readonly RDCoreDiagnosticCode Duplicate = new("XYZ", 5);
    private static readonly Uri ProjectFile = new("file:///c:/ws/.rdproj");
    private static readonly Uri Orders = new("file:///c:/ws/Orders.cls");
    private static readonly Uri Invoices = new("file:///c:/ws/Invoices.cls");
    private static readonly Uri Customers = new("file:///c:/ws/Customers.cls");

    public TestContext TestContext { get; set; } = null!;

    // an analyzer that reports its code in Orders and in Invoices, with a fix that inserts a line there.
    private sealed class Analyzer : IProjectModulesAnalyzer
    {
        public IReadOnlyList<RDCoreDiagnosticCode> Codes => [Duplicate];

        public IEnumerable<AnalyzerFinding> Analyze(ProjectModulesAnalysisContext context)
            => [.. new[] { Orders, Invoices }.Select(document => new AnalyzerFinding(Duplicate, new SourceRange(2, 0, 2, 4), DiagnosticSeverity.Error, "Twice.")
            {
                Document = document,
                Fixes = new DiagnosticFixes
                {
                    Edits = [new EditFix { Title = "Add a line", Edits = [new TextEdit { NewText = "'@Line\n", Range = new(2, 0, 2, 0) }] }],
                },
            })];
    }

    private async Task<DiagnoseProjectModulesResult> DiagnoseAsync()
    {
        var handler = new ProjectModulesAnalyzerDiagnosticsHandler(
            new DiagnosticFactory(), [new Analyzer()], NullLogger<ProjectModulesAnalyzerDiagnosticsHandler>.Instance);
        var payload = new DiagnoseProjectModulesPayload(ProjectFile, new RDCoreProject { Name = "Billing" },
        [
            new DiagnoseDocumentPayload(Orders, 7, new ModuleParseResult()),
            new DiagnoseDocumentPayload(Invoices, 3, new ModuleParseResult()),
            new DiagnoseDocumentPayload(Customers, 9, new ModuleParseResult()),
        ]);
        return await handler.Handle(new DiagnoseProjectModulesParams { Json = PlatformJson.Serialize(payload) }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task EachModulesDiagnostics_AreUnderIt_WithItsVersion_AndAModuleWithNone_IsNotListed()
    {
        var result = await DiagnoseAsync();

        CollectionAssert.AreEquivalent(
            new[] { ("Orders.cls", 7, "XYZ00005"), ("Invoices.cls", 3, "XYZ00005") },
            result.Modules.Select(module => (module.Document.Segments[^1], module.SourceVersion, module.Diagnostics.Single().Code?.String)).ToArray());
    }

    [TestMethod]
    public async Task AFindingsFix_IsOfTheVersionOfTheModuleItWasFoundIn()
    {
        var result = await DiagnoseAsync();

        var invoices = result.Modules.Single(module => module.Document == Invoices);
        Assert.AreEqual(3, DiagnosticFixes.Of(invoices.Diagnostics.Single())!.Edits.Single().Version);
    }
}
