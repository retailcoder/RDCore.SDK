using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.Diagnostics;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The handler a diagnostics-provider extension registers for <c>rdcore/diagnostics/document</c> (<see cref="AnalyzerDiagnosticsHandler"/>): it runs the
/// extension's analyzers on the document the language server pushes down, and answers with their findings as diagnostics, of the document's version.
/// </summary>
[TestClass]
public sealed class AnalyzerDiagnosticsHandlerTests
{
    private static readonly RDCoreDiagnosticCode Implicit = new("XYZ", 1);
    private static readonly RDCoreDiagnosticCode Several = new("XYZ", 2);
    private static readonly Uri Document = new("file:///c:/ws/Orders.cls");

    public TestContext TestContext { get; set; } = null!;

    // an analyzer that reports each of its codes that the document enables, at its own line, with a fix that inserts a line there.
    private sealed class Analyzer(params RDCoreDiagnosticCode[] codes) : IModuleAnalyzer
    {
        public IReadOnlyList<RDCoreDiagnosticCode> Codes => codes;

        public IEnumerable<AnalyzerFinding> Analyze(ModuleAnalysisContext context)
            => [.. codes.Where(context.IsEnabled).Select(code => new AnalyzerFinding(code, new SourceRange(code.Number, 0, code.Number, 4),
                DiagnosticSeverity.Information, $"Found {code}.")
            {
                Fixes = new DiagnosticFixes
                {
                    Edits =
                    [
                        new EditFix { Title = "Add a line", Edits = [new TextEdit { NewText = "'@Line\n", Range = new(code.Number, 0, code.Number, 0) }] },
                    ],
                },
            })];
    }

    private async Task<DiagnoseDocumentResult> DiagnoseAsync(int version, params RDCoreDiagnosticCode[] disabled)
    {
        var handler = new AnalyzerDiagnosticsHandler(
            new DiagnosticFactory(), [new Analyzer(Implicit, Several)], NullLogger<AnalyzerDiagnosticsHandler>.Instance);
        var payload = new DiagnoseDocumentPayload(Document, version, new ModuleParseResult()) { DisabledCodes = disabled };
        return await handler.Handle(new DiagnoseDocumentParams { Json = PlatformJson.Serialize(payload) }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task TheAnalyzersFindings_AreTheDiagnostics_AndTheDocumentsVersionIsEchoed()
    {
        var result = await DiagnoseAsync(version: 7);

        CollectionAssert.AreEqual(new[] { "XYZ00001", "XYZ00002" }, result.Diagnostics.Select(diagnostic => diagnostic.Code?.String).ToArray());
        Assert.AreEqual(DiagnosticSeverity.Information, result.Diagnostics.First().Severity);
        Assert.AreEqual(7, result.SourceVersion);
    }

    [TestMethod]
    public async Task AFindingsFix_IsOfTheVersionOfTheDocumentItWasFoundIn()
    {
        var result = await DiagnoseAsync(version: 7);

        var fix = DiagnosticFixes.Of(result.Diagnostics.First())!.Edits.Single();
        Assert.AreEqual(7, fix.Version);
        Assert.AreEqual("'@Line\n", fix.Edits.Single().NewText);
    }

    [TestMethod]
    public async Task ACodeTheDocumentDisables_IsNotReported()
    {
        var result = await DiagnoseAsync(version: 1, Several);

        CollectionAssert.AreEqual(new[] { "XYZ00001" }, result.Diagnostics.Select(diagnostic => diagnostic.Code?.String).ToArray());
    }
}
