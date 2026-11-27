using System.Globalization;
using RDCore.SDK.Extensibility.Annotations;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Completion;
using RDCore.SDK.Workspace;
using RDCore.SDK.Workspace.Forms;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// What completion offers for an annotation being written: where one is being written on a line, what an annotation written at a place binds to, and the
/// annotations of an extension legal there, each with its description and its signature in the language of the user interface.
/// </summary>
[TestClass]
public sealed class AnnotationCompletionTests
{
    private static readonly Uri File = new("file:///c:/ws/Module1.bas");
    private static int _next;

    private static SyntaxNodeId Id() => new(File.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation Lines(int first, int last, int startCharacter = 0) => new()
    {
        Uri = File,
        Range = new SourceRange(first, startCharacter, last, 40),
    };

    private static MemberDeclarationNode Procedure(int first, int last) => new(Id(), Lines(first, last), [], $"P{first}", MemberKind.Procedure);

    private static VariableDeclarationNode Variable(int line) => new(Id(), Lines(line, line), $"V{line}", []);

    // the probe on its comment line, as the parser keeps it: the comment, then the annotation inside its range.
    private static SyntaxNode[] Probe(int line, int startCharacter = 0) =>
    [
        new CommentTriviaNode(Id(), Lines(line, line, startCharacter), $"'@{AnnotationCompletion.Probe}"),
        new AnnotationTriviaNode(Id(), Lines(line, line, startCharacter), AnnotationCompletion.Probe, []),
    ];

    private static RDCoreDocument Document(SyntaxNode[] children, SyntaxNode[] trivia, FormDescriptor? designer = null)
        => new(File, new ModuleParseResult { SyntaxTree = new ModuleNode(Id(), Lines(0, 30), [.. children]), Trivia = [.. trivia], Designer = designer });

    private static AnnotationSite OwnLine => new(0, string.Empty, null, IsOnItsOwnLine: true);

    [TestMethod]
    [DataRow("'@", "")]
    [DataRow("    '@Desc", "Desc")]
    [DataRow("x = 1 '@Desc", "Desc")]
    [DataRow("s = \"it's\" '@Pre", "Pre")]
    [DataRow("Dim x@", null)]
    [DataRow("s = \"'@", null)]
    [DataRow("' some text '@", null)]
    [DataRow("Rem '@", null)]
    [DataRow("' @", null)]
    [DataRow("'@1", null)]
    public void AnAnnotationsName_IsBeingWritten_OnlyRightAfterTheQuoteThatStartsAComment(string text, string? name)
    {
        var site = AnnotationSite.Read(text);

        Assert.AreEqual(name, site?.Name);
        Assert.IsNull(site?.ArgumentIndex);
    }

    [TestMethod]
    [DataRow("'@Pre", true)]
    [DataRow("    '@Pre", true)]
    [DataRow("x = 1 '@Pre", false)]
    public void AnAnnotation_IsOnItsOwnLine_WhereNothingButWhiteSpaceIsBeforeItsComment(string text, bool isOnItsOwnLine)
        => Assert.AreEqual(isOnItsOwnLine, AnnotationSite.Read(text)!.IsOnItsOwnLine);

    [TestMethod]
    [DataRow("'@Route(", 0)]
    [DataRow("'@Route(\"a, b\", ", 1)]
    [DataRow("'@Route(Left(1, 2), ", 1)]
    [DataRow("'@IgnoreModule RDC01001, RDC01002, ", 2)]
    [DataRow("'@Route(\"a\")", null)]
    [DataRow("'@Route)", null)]
    public void TheArgumentBeingWritten_IsTheOneAfterTheCommasOfTheArgumentList(string text, int? index)
    {
        var site = AnnotationSite.Read(text);

        Assert.AreEqual(index, site?.ArgumentIndex);
        if (index is not null)
        {
            Assert.AreEqual(text[2..text.IndexOfAny(['(', ' '])], site!.Name);
        }
    }

    [TestMethod]
    public void TheProbeLine_KeepsTheCodeBeforeTheComment_AndTheLineEnd()
    {
        const string Line = "x = 1 '@Desc\r";

        Assert.AreEqual($"x = 1 '@{AnnotationCompletion.Probe}\r", AnnotationCompletion.ProbeLine(Line, AnnotationSite.Read(Line[..^1])!));
    }

    // the module: a procedure on lines 4-6 and another on 9-11; the probe on the line given.
    [TestMethod]
    [DataRow(1, AnnotationTargets.Module | AnnotationTargets.Line)]
    [DataRow(3, AnnotationTargets.Module | AnnotationTargets.Member | AnnotationTargets.Line)]
    [DataRow(5, AnnotationTargets.Line)]
    [DataRow(8, AnnotationTargets.Member | AnnotationTargets.Line)]
    public void AnAnnotation_AnnotatesWhatItIsWrittenAbove(int line, AnnotationTargets targets)
    {
        var document = Document([Procedure(4, 6), Procedure(9, 11)], Probe(line));

        Assert.AreEqual(targets, AnnotationCompletion.PlaceOf(document, ModuleType.StdModule, OwnLine, line)?.Targets);
    }

    [TestMethod]
    public void AnAnnotationDirectlyAboveAVariableOfTheModule_AnnotatesTheVariable()
    {
        var document = Document([Variable(2), Procedure(4, 6)], Probe(1));

        Assert.AreEqual(AnnotationTargets.Module | AnnotationTargets.Variable | AnnotationTargets.Line,
            AnnotationCompletion.PlaceOf(document, ModuleType.StdModule, OwnLine, 1)?.Targets);
    }

    [TestMethod]
    public void AnAnnotationAfterCodeOnItsLine_InAProcedure_AnnotatesNothing()
    {
        var document = Document([Procedure(4, 6)], Probe(5, startCharacter: 10));
        var afterCode = OwnLine with { IsOnItsOwnLine = false };

        Assert.AreEqual(AnnotationTargets.None, AnnotationCompletion.PlaceOf(document, ModuleType.StdModule, afterCode, 5)?.Targets);
    }

    [TestMethod]
    public void NoPlace_IsAtALineWithNoProbe()
        => Assert.IsNull(AnnotationCompletion.PlaceOf(Document([Procedure(4, 6)], Probe(1)), ModuleType.StdModule, OwnLine, 2));

    [TestMethod]
    [DataRow(ModuleType.StdModule, false, AnnotationModuleKinds.Standard)]
    [DataRow(ModuleType.ClassModule, false, AnnotationModuleKinds.Class)]
    [DataRow(ModuleType.ClassModule, true, AnnotationModuleKinds.Form)]
    [DataRow(ModuleType.DocumentModule, false, AnnotationModuleKinds.Document)]
    public void AModulesKind_IsItsType_AClassWithADesignerBeingAForm(ModuleType type, bool hasDesigner, AnnotationModuleKinds kind)
    {
        var designer = new FormDescriptor("Form1", FormDesignerKind.RDForms, new FormControlClass("RDForms", "Form"), new Dictionary<string, string>(), []);
        var document = Document([Procedure(4, 6)], Probe(1), hasDesigner ? designer : null);

        Assert.AreEqual(kind, AnnotationCompletion.PlaceOf(document, type, OwnLine, 1)?.Kind);
    }

    // an extension's annotations, as its manifest carries them: described in English and in French.
    private static readonly AnnotationDeclaration Route = new()
    {
        Name = "Route",
        Targets = AnnotationTargets.Member,
        ModuleKinds = AnnotationModuleKinds.Standard | AnnotationModuleKinds.Class,
        Parameters =
        [
            new() { Name = "Path", Takes = AnnotationArgumentKind.Text },
            new() { Name = "Verbs", Takes = AnnotationArgumentKind.Identifier, IsOptional = true, Repeats = true },
        ],
    };

    private static readonly AnnotationDeclaration Mounted = new()
    {
        Name = "Mounted",
        Targets = AnnotationTargets.Module,
        ModuleKinds = AnnotationModuleKinds.Class,
    };

    private static readonly AnnotationDeclaration Old = new() { Name = "Old", Targets = AnnotationTargets.Member, IsDeprecated = true, ReplacedBy = "Route" };

    private static readonly AnnotationCatalogue Catalogue = new(
    [
        new AnnotationSet
        {
            Declarations = [Route, Mounted, Old],
            Descriptions = new()
            {
                ["en"] = new()
                {
                    ["Route"] = "Routes a request.", ["Route_Path"] = "The path.", ["Route_Verbs"] = "The verbs.", ["Mounted"] = "Mounts it.",
                },
                ["fr"] = new() { ["Route"] = "Achemine une requête.", ["Route_Path"] = "Le chemin.", ["Route_Verbs"] = "Les verbes." },
            },
        },
    ]);

    [TestMethod]
    [DataRow(AnnotationTargets.Member | AnnotationTargets.Line, AnnotationModuleKinds.Standard, "Route")]
    [DataRow(AnnotationTargets.Module | AnnotationTargets.Line, AnnotationModuleKinds.Class, "Mounted")]
    [DataRow(AnnotationTargets.Module | AnnotationTargets.Line, AnnotationModuleKinds.Standard, "")]
    [DataRow(AnnotationTargets.Module | AnnotationTargets.Member, AnnotationModuleKinds.Class, "Route,Mounted")]
    public void TheAnnotationsOffered_AreTheOnesLegalAtThePlace_NoneDeprecated(AnnotationTargets targets, AnnotationModuleKinds kind, string offered)
    {
        var candidates = AnnotationCompletion.CandidatesAt(Catalogue, new AnnotationPlace(File, kind, targets), CultureInfo.InvariantCulture);

        Assert.AreEqual(offered, string.Join(",", candidates.Select(candidate => candidate.Label)));
    }

    [TestMethod]
    [DataRow("en-CA", "Routes a request.")]
    [DataRow("fr-CA", "Achemine une requête.")]
    public void AnOfferedAnnotation_IsDescribedInTheLanguageOfTheUserInterface_ItsSignatureAsItsDetail(string culture, string description)
    {
        var place = new AnnotationPlace(File, AnnotationModuleKinds.Standard, AnnotationTargets.Member);

        var route = AnnotationCompletion.CandidatesAt(Catalogue, place, CultureInfo.GetCultureInfo(culture)).Single(candidate => candidate.Label == Route.Name);

        Assert.AreEqual(description, route.Documentation);
        Assert.AreEqual("@Route(Path, ParamArray Verbs())", route.Detail);
    }

    [TestMethod]
    public void AnAnnotationsSignature_WritesEachParameterAsAProceduresWouldBe_AndDescribesIt()
    {
        var signature = AnnotationCompletion.SignatureOf(Catalogue, Route, CultureInfo.GetCultureInfo("fr-CA"));

        Assert.AreEqual("Achemine une requête.", signature.Documentation);
        CollectionAssert.AreEqual(new[] { "Path", "ParamArray Verbs()" }, signature.Parameters.Select(parameter => parameter.TextIn(signature)).ToArray());
        CollectionAssert.AreEqual(new[] { "Le chemin.", "Les verbes." }, signature.Parameters.Select(parameter => parameter.Documentation).ToArray());
        Assert.IsTrue(signature.Parameters[1].IsOptional);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(4, 1)]
    public void EveryArgumentFromAListsOn_IsGivenToTheList(int argument, int parameter)
        => Assert.AreEqual(parameter, AnnotationCompletion.SignatureOf(Catalogue, Route, CultureInfo.InvariantCulture).ActiveParameterOf(argument));

    [TestMethod]
    public void AnOptionalParameter_IsWrittenOptional()
    {
        var obsolete = new AnnotationDeclaration
        {
            Name = "Obsolete",
            Targets = AnnotationTargets.Member,
            Parameters = [new() { Name = "Message", Takes = AnnotationArgumentKind.Text, IsOptional = true }],
        };

        Assert.AreEqual("@Obsolete(Optional Message)", AnnotationCompletion.SignatureOf(Catalogue, obsolete, CultureInfo.InvariantCulture).Label);
    }
}
