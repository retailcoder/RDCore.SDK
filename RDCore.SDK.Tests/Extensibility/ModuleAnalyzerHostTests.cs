using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;
using RDCore.SDK.Semantics.Flags;
using RDCore.SDK.Workspace.Forms;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The analyzers of a document are run as the platform runs them for every extension: those of the phase asked for, and of a code that is not disabled. An
/// analyzer is told which of its codes are, and skips their work; one whose every code is disabled is not run. A finding of a disabled code that an analyzer
/// reports anyway is dropped, and the analyzer named in a warning. A code of the language's own errors cannot be disabled. An analyzer is told the kind of the
/// module: the language server's word for it, or what the module's parse and the host's facts say of it.
/// </summary>
[TestClass]
public sealed class ModuleAnalyzerHostTests
{
    private static readonly RDCoreDiagnosticCode Named = new("RDC", 1001);
    private static readonly RDCoreDiagnosticCode Hungarian = new("RDC", 1002);
    private static readonly RDCoreDiagnosticCode Other = new("XYZ", 42);
    private static readonly Uri Document = new("file:///c:/ws/Module1.bas");

    private readonly RecordingLogger _logger = new();

    // an analyzer that reports each of its codes it is told is enabled, or every one of them when it does not ask; how many times it ran.
    private sealed class Analyzer(IReadOnlyList<RDCoreDiagnosticCode> codes, AnalysisPhase phase = AnalysisPhase.Static, bool asks = true) : IModuleAnalyzer
    {
        public int Runs { get; private set; }

        public IReadOnlyList<RDCoreDiagnosticCode> Codes => codes;

        public AnalysisPhase Phase => phase;

        public IEnumerable<AnalyzerFinding> Analyze(ModuleAnalysisContext context)
        {
            Runs++;
            return [.. codes.Where(code => !asks || context.IsEnabled(code)).Select(code => new AnalyzerFinding(code, default, DiagnosticSeverity.Warning, code.ToString()))];
        }
    }

    private sealed class Failing : IModuleAnalyzer
    {
        public IReadOnlyList<RDCoreDiagnosticCode> Codes => [Other];

        public IEnumerable<AnalyzerFinding> Analyze(ModuleAnalysisContext context) => throw new InvalidOperationException("no");
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

    private static ModuleAnalysisContext Context(params RDCoreDiagnosticCode[] disabled)
        => new(Document, new ModuleParseResult(), Semantics: null) { DisabledCodes = disabled.ToHashSet() };

    private static string[] Codes(IEnumerable<AnalyzerFinding> findings) => [.. findings.Select(finding => finding.Code.ToString()).Order()];

    [TestMethod]
    public void AnAnalyzerWhoseEveryCodeIsDisabled_IsNotRun()
    {
        var naming = new Analyzer([Named, Hungarian]);
        var other = new Analyzer([Other]);

        var findings = ModuleAnalyzerHost.Run([naming, other], Context(Named, Hungarian), AnalysisPhase.All, _logger);

        Assert.AreEqual(0, naming.Runs);
        Assert.AreEqual(1, other.Runs);
        CollectionAssert.AreEqual(new[] { "XYZ00042" }, Codes(findings));
        Assert.IsEmpty(_logger.Warnings);
    }

    [TestMethod]
    public void AnAnalyzerWithOneOfItsCodesDisabled_IsRun_AndToldWhichSoItSkipsItsWork()
    {
        var naming = new Analyzer([Named, Hungarian]);

        var findings = ModuleAnalyzerHost.Run([naming], Context(Hungarian), AnalysisPhase.All, _logger);

        Assert.AreEqual(1, naming.Runs);
        CollectionAssert.AreEqual(new[] { "RDC01001" }, Codes(findings));
        Assert.IsEmpty(_logger.Warnings, "it asked: nothing was dropped");
    }

    [TestMethod]
    public void AFindingOfADisabledCode_ThatAnAnalyzerReportsAnyway_IsDropped_AndTheAnalyzerNamedInAWarning()
    {
        var careless = new Analyzer([Named, Hungarian], asks: false);

        var findings = ModuleAnalyzerHost.Run([careless], Context(Hungarian), AnalysisPhase.All, _logger);

        CollectionAssert.AreEqual(new[] { "RDC01001" }, Codes(findings));
        Assert.HasCount(1, _logger.Warnings);
        StringAssert.Contains(_logger.Warnings[0], nameof(Analyzer));
        StringAssert.Contains(_logger.Warnings[0], "RDC01002");
    }

    [TestMethod]
    [DataRow("VBC", DisplayName = "a compile error")]
    [DataRow("VBR", DisplayName = "a run-time error")]
    [DataRow("VBA", DisplayName = "an error the program raises")]
    public void ACodeOfTheLanguagesOwnErrors_CannotBeDisabled(string prefix)
    {
        var language = new RDCoreDiagnosticCode(prefix, 13);
        var analyzer = new Analyzer([language]);

        Assert.IsFalse(language.CanBeDisabled);
        Assert.IsTrue(Context(language).IsEnabled(language));
        CollectionAssert.AreEqual(new[] { language.ToString() }, Codes(ModuleAnalyzerHost.Run([analyzer], Context(language), AnalysisPhase.All, _logger)));
        Assert.IsTrue(Named.CanBeDisabled);
        Assert.IsTrue(Other.CanBeDisabled, "an extension's code, as the platform's analyzers' codes");
    }

    [TestMethod]
    public void TheAnalyzersOfAnotherPhase_AreNotRun()
    {
        var early = new Analyzer([Named]);
        var late = new Analyzer([Other], AnalysisPhase.Runtime);

        var findings = ModuleAnalyzerHost.Run([early, late], Context(), AnalysisPhase.Static, _logger);

        Assert.AreEqual((1, 0), (early.Runs, late.Runs));
        CollectionAssert.AreEqual(new[] { "RDC01001" }, Codes(findings));
    }

    [TestMethod]
    public void AnAnalyzerThatFails_IsNamedInAWarning_AndTheOthersFindingsAreKept()
    {
        var findings = ModuleAnalyzerHost.Run([new Failing(), new Analyzer([Named])], Context(), AnalysisPhase.All, _logger);

        CollectionAssert.AreEqual(new[] { "RDC01001" }, Codes(findings));
        Assert.HasCount(1, _logger.Warnings);
        StringAssert.Contains(_logger.Warnings[0], nameof(Failing));
    }

    [TestMethod]
    public void TheCodesDisabledForADocument_TravelWithItsPayload_ToTheAnalyzersContext()
    {
        var sent = new DiagnoseDocumentPayload(Document, 3, new ModuleParseResult(), Phase: AnalysisPhase.Static) { DisabledCodes = [Hungarian, Other] };
        var payload = PlatformJson.Deserialize<DiagnoseDocumentPayload>(PlatformJson.Serialize(sent));
        var naming = new Analyzer([Named, Hungarian]);
        var other = new Analyzer([Other]);

        var findings = ModuleAnalyzerHost.Run([naming, other], payload, _logger);

        CollectionAssert.AreEqual(new[] { "RDC01002", "XYZ00042" }, payload.DisabledCodes.Select(code => code.ToString()).Order().ToArray());
        Assert.AreEqual((1, 0), (naming.Runs, other.Runs));
        CollectionAssert.AreEqual(new[] { "RDC01001" }, Codes(findings));
    }

    // a code an analyzer does not declare would not keep it running were its declared codes all disabled: the finding is reported, and the analyzer named.
    private sealed class Undeclaring : IModuleAnalyzer
    {
        public IReadOnlyList<RDCoreDiagnosticCode> Codes => [Named];

        public IEnumerable<AnalyzerFinding> Analyze(ModuleAnalysisContext context)
            => [new(Named, default, DiagnosticSeverity.Warning, "declared"), new(Other, default, DiagnosticSeverity.Warning, "not declared")];
    }

    [TestMethod]
    public void AFindingOfACodeTheAnalyzerDoesNotDeclare_IsReported_AndTheAnalyzerNamedInAWarning()
    {
        var findings = ModuleAnalyzerHost.Run([new Undeclaring()], Context(), AnalysisPhase.All, _logger);

        CollectionAssert.AreEqual(new[] { "RDC01001", "XYZ00042" }, Codes(findings));
        Assert.HasCount(1, _logger.Warnings);
        StringAssert.Contains(_logger.Warnings[0], nameof(Undeclaring));
        StringAssert.Contains(_logger.Warnings[0], "XYZ00042");
    }

    [TestMethod]
    [DataRow("en", SettingScope.Platform, "Disable RDC01001 in every workspace")]
    [DataRow("en", SettingScope.Workspace, "Disable RDC01001 in this workspace")]
    [DataRow("fr", SettingScope.Platform, "Désactiver RDC01001 dans tous les espaces de travail")]
    [DataRow("fr", SettingScope.Workspace, "Désactiver RDC01001 dans cet espace de travail")]
    public void TheCodeActionThatDisablesACode_IsTitledInTheLanguageOfTheReader(string language, SettingScope scope, string title)
        => Assert.AreEqual(title, RDCore.SDK.Semantics.Refactoring.RefactoringTitles.DisableDiagnostic(Named, scope, System.Globalization.CultureInfo.GetCultureInfo(language)));

    [TestMethod]
    public void ByDefault_NoCodeIsDisabled()
    {
        Assert.IsEmpty(new DiagnoseDocumentPayload(Document, 1, new ModuleParseResult()).DisabledCodes);
        Assert.IsTrue(new ModuleAnalysisContext(Document, new ModuleParseResult(), null).IsEnabled(Named));
    }

    // an analyzer that keeps the kind of the module it is given.
    private sealed class KindReader : IModuleAnalyzer
    {
        public ModuleType? Kind { get; private set; }

        public IReadOnlyList<RDCoreDiagnosticCode> Codes => [];

        public IEnumerable<AnalyzerFinding> Analyze(ModuleAnalysisContext context)
        {
            Kind = context.ModuleKind;
            return [];
        }
    }

    [TestMethod]
    [DataRow(ModuleType.ClassModule)]
    [DataRow(ModuleType.DocumentModule)]
    public void TheKindTheLanguageServerSays_TravelsWithItsPayload_ToTheAnalyzersContext(ModuleType kind)
    {
        // the parse of a standard module, which a document module's export and a class block's parse look like.
        var sent = new DiagnoseDocumentPayload(Document, 1, new ModuleParseResult()) { ModuleKind = kind };
        var payload = PlatformJson.Deserialize<DiagnoseDocumentPayload>(PlatformJson.Serialize(sent));
        var reader = new KindReader();

        ModuleAnalyzerHost.Run([reader], payload, _logger);

        Assert.AreEqual(kind, reader.Kind);
    }

    private static int _node;

    private static ModuleHeaderTriviaNode Header(string source)
        => new(new SyntaxNodeId(Document.ToString(), [Interlocked.Increment(ref _node)]), new SourceLocation { Uri = Document, Range = new(0, 0, 0, 1) }, source);

    private static ModuleSemanticsDto Semantics(ClassModuleSemanticFlags? flags = null, DocumentModuleFact? document = null)
        => new(Document, true, [], [], [], Class: flags, Document: document);

    [TestMethod]
    [DataRow("nothing", ModuleType.StdModule)]
    [DataRow("a header with no VERSION line", ModuleType.StdModule)]
    [DataRow("a VERSION header", ModuleType.ClassModule)]
    [DataRow("a designer", ModuleType.ClassModule)]
    [DataRow("the file of an RDForms form's code", ModuleType.ClassModule)]
    [DataRow("the host's class flags", ModuleType.ClassModule)]
    [DataRow("the host's document fact", ModuleType.DocumentModule)]
    [DataRow("the host's document fact and class flags", ModuleType.DocumentModule)]
    public void WhenTheLanguageServerDoesNotSay_TheKindIsReadFromWhatTheAnalyzerIsGiven(string given, ModuleType kind)
    {
        var designer = new FormDescriptor("Form1", FormDesignerKind.RDForms, new FormControlClass("RDForms", "Form"), new Dictionary<string, string>(), []);
        var document = new DocumentModuleFact(null, null, []);
        var context = given switch
        {
            "a header with no VERSION line" => Context() with { ParseResult = new() { Trivia = [Header("BEGIN\r\n  MultiUse = -1  'True\r\nEND")] } },
            "a VERSION header" => Context() with { ParseResult = new() { Trivia = [Header("VERSION 1.0 CLASS\r\n")] } },
            "a designer" => Context() with { ParseResult = new() { Designer = designer } },
            "the file of an RDForms form's code" => Context() with { Document = new Uri("file:///c:/ws/Form1.rdform") },
            "the host's class flags" => Context() with { Semantics = Semantics(flags: ClassModuleSemanticFlags.None) },
            "the host's document fact" => Context() with { Semantics = Semantics(document: document) },
            "the host's document fact and class flags" => Context() with { Semantics = Semantics(ClassModuleSemanticFlags.None, document) },
            _ => Context(),
        };
        var reader = new KindReader();

        ModuleAnalyzerHost.Run([reader], new DiagnoseDocumentPayload(context.Document, 1, context.ParseResult, context.Semantics), _logger);

        Assert.AreEqual(kind, reader.Kind);
    }
}
