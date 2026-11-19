using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using System.Collections.Immutable;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// Whole lines placed by the parse of a module: a node's lines are removable when nothing else the parse has a range for is written on them, and lines of
/// their own are written before or after a node, ended with the module's line ending.
/// </summary>
/// <remarks>
/// The parse is of this procedure, each statement a keyword statement:
/// <code>
/// 0 Sub Main()
/// 1     Beep
/// 2     Beep: Stop
/// 3     Beep ' note
/// 4     If x Then Beep
/// 5     Stop
/// 6 End Sub
/// </code>
/// </remarks>
[TestClass]
public sealed class LineEditsTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");
    private static int _next;

    private static SyntaxNodeId Id() => new(Module.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation At(int startLine, int startCharacter, int endLine, int endCharacter)
        => new() { Uri = Module, Range = new SourceRange(startLine, startCharacter, endLine, endCharacter) };

    private static KeywordStatementNode Statement(string keyword, int line, int character)
        => new(Id(), At(line, character, line, character + keyword.Length), keyword, []);

    private static readonly KeywordStatementNode Alone = Statement("Beep", 1, 4);
    private static readonly KeywordStatementNode BeforeAColon = Statement("Beep", 2, 4);
    private static readonly KeywordStatementNode AfterAColon = Statement("Stop", 2, 10);
    private static readonly KeywordStatementNode BeforeAComment = Statement("Beep", 3, 4);
    private static readonly KeywordStatementNode InASingleLineIf = Statement("Beep", 4, 14);
    private static readonly KeywordStatementNode Last = Statement("Stop", 5, 4);

    private static readonly IfBlockStatementNode SingleLineIf = new(Id(), At(4, 4, 4, 18), new SimpleNameExpressionNode(Id(), At(4, 7, 4, 8), "x"),
        new StatementBlock([InASingleLineIf]), [], null);

    private static ModuleParseResult Parse(SourcePosition endOfText, string lineEnding = "\r\n")
    {
        var member = new MemberDeclarationNode(Id(), At(0, 0, 6, 7), [Alone, BeforeAColon, AfterAColon, BeforeAComment, SingleLineIf, Last], "Main", MemberKind.Procedure);
        return new ModuleParseResult
        {
            SyntaxTree = new ModuleNode(Id(), new SourceLocation { Uri = Module, Range = new SourceRange(SourcePosition.Zero, endOfText) }, [member]),
            Trivia = [new CommentTriviaNode(Id(), At(3, 9, 3, 15), "' note")],
            LineEnding = lineEnding,
        };
    }

    private static readonly ModuleParseResult EndsWithALineEnd = Parse(new SourcePosition(7, 0));

    [TestMethod]
    public void TheLinesOfAStatementAlone_RunToTheStartOfTheNextLine()
        => Assert.AreEqual(new SourceRange(1, 0, 2, 0), LineEdits.WholeLinesOf(EndsWithALineEnd, Alone));

    [TestMethod]
    public void TheLinesOfTheLastStatementOfABody_RunToTheStartOfItsEnd()
        => Assert.AreEqual(new SourceRange(5, 0, 6, 0), LineEdits.WholeLinesOf(EndsWithALineEnd, Last));

    [TestMethod]
    public void ASpanOfStatementsAlone_IsWholeLines()
        => Assert.AreEqual(new SourceRange(4, 0, 6, 0), LineEdits.WholeLinesOf(EndsWithALineEnd, new SourceRange(4, 4, 5, 8)));

    [TestMethod]
    public void AModuleNodeOfNoRange_EndsNoEarlierThanWhatIsWrittenInIt()
        => Assert.AreEqual(new SourceRange(1, 0, 2, 0), LineEdits.WholeLinesOf(Parse(SourcePosition.Zero), Alone));

    [TestMethod]
    public void AStatementBeforeAnotherOnItsLine_IsNotWholeLines()
        => Assert.IsNull(LineEdits.WholeLinesOf(EndsWithALineEnd, BeforeAColon));

    [TestMethod]
    public void AStatementAfterAnotherOnItsLine_IsNotWholeLines()
        => Assert.IsNull(LineEdits.WholeLinesOf(EndsWithALineEnd, AfterAColon));

    [TestMethod]
    public void AStatementBeforeAComment_IsNotWholeLines()
        => Assert.IsNull(LineEdits.WholeLinesOf(EndsWithALineEnd, BeforeAComment));

    [TestMethod]
    public void AStatementOfASingleLineIf_IsNotWholeLines()
        => Assert.IsNull(LineEdits.WholeLinesOf(EndsWithALineEnd, InASingleLineIf));

    [TestMethod]
    public void AStatementALineContinuationCarriesOntoItsLine_IsNotWholeLines()
    {
        var parse = EndsWithALineEnd with { Trivia = [new LineContinuationTriviaNode(Id(), At(0, 10, 1, 4))] };

        Assert.IsNull(LineEdits.WholeLinesOf(parse, Alone));
    }

    [TestMethod]
    public void AStatementInsideAnIfDirective_IsWholeLines()
    {
        var directive = new PrecompilerIfBlockStatementNode(Id(), At(0, 11, 2, 0), []);
        var parse = EndsWithALineEnd with { PrecompilerTrivia = [new PrecompilerTriviaNode(Id(), At(0, 0, 7, 0), [directive], string.Empty)] };

        Assert.AreEqual(new SourceRange(1, 0, 2, 0), LineEdits.WholeLinesOf(parse, Alone));
    }

    [TestMethod]
    public void TheLastLinesOfATextWithoutALineEnd_RunToTheEndOfTheText()
    {
        var parse = Parse(new SourcePosition(6, 7));
        var member = (MemberDeclarationNode)parse.SyntaxTree!.Children[0];

        Assert.AreEqual(new SourceRange(0, 0, 6, 7), LineEdits.WholeLinesOf(parse, member));
    }

    [TestMethod]
    public void LinesBeforeANode_AreWrittenAtTheStartOfItsLine_EachEndedWithTheModulesLineEnding()
    {
        var edit = LineEdits.InsertLinesBefore(EndsWithALineEnd, Alone, "Dim n As Long", "");

        Assert.AreEqual(new Range(1, 0, 1, 0), edit.Range);
        Assert.AreEqual("Dim n As Long\r\n\r\n", edit.NewText);
    }

    [TestMethod]
    public void LinesAfterANode_AreWrittenAtTheStartOfTheNextLine()
    {
        var edit = LineEdits.InsertLinesAfter(Parse(new SourcePosition(7, 0), "\n"), Last, "Beep");

        Assert.AreEqual(new Range(6, 0, 6, 0), edit.Range);
        Assert.AreEqual("Beep\n", edit.NewText);
    }

    [TestMethod]
    public void LinesAfterTheLastLineOfATextWithoutALineEnd_AreWrittenAtItsEnd_EachAfterALineEnd()
    {
        var parse = Parse(new SourcePosition(6, 7));
        var member = (MemberDeclarationNode)parse.SyntaxTree!.Children[0];

        var edit = LineEdits.InsertLinesAfter(parse, member, "", "Sub Other()");

        Assert.AreEqual(new Range(6, 7, 6, 7), edit.Range);
        Assert.AreEqual("\r\n\r\nSub Other()", edit.NewText);
    }

    // 0 Option Explicit
    // 1 Private n As Long
    // 2 (blank, or the comment)
    // 3 ' about Main / Sub Main()
    private static ModuleParseResult Declarations(bool blankLine, bool withProcedure = true, SourcePosition? endOfText = null)
    {
        var option = Statement("Option Explicit", 0, 0);
        var field = new KeywordStatementNode(Id(), At(1, 0, 1, 17), "Private", []);
        var commentLine = blankLine ? 3 : 2;
        var main = new MemberDeclarationNode(Id(), At(commentLine + 1, 0, commentLine + 2, 7), [], "Main", MemberKind.Procedure);
        var end = endOfText ?? new SourcePosition(withProcedure ? commentLine + 3 : 2, 0);
        return new ModuleParseResult
        {
            SyntaxTree = new ModuleNode(Id(), new SourceLocation { Uri = Module, Range = new SourceRange(SourcePosition.Zero, end) },
                withProcedure ? [option, field, main] : [option, field]),
            Trivia = withProcedure ? [new CommentTriviaNode(Id(), At(commentLine, 0, commentLine, 12), "' about Main")] : [],
        };
    }

    [TestMethod]
    public void TheDeclarations_EndAfterTheLastLineWrittenBeforeTheFirstProcedureAndTheCommentsAboveIt()
        => Assert.AreEqual(new SourcePosition(2, 0), LineEdits.EndOfDeclarations(Declarations(blankLine: true)));

    [TestMethod]
    public void TheDeclarationsOfAModuleWithNoProcedure_EndAtTheEndOfTheText()
        => Assert.AreEqual(new SourcePosition(2, 0), LineEdits.EndOfDeclarations(Declarations(blankLine: true, withProcedure: false)));

    [TestMethod]
    public void LinesAfterTheDeclarations_BeforeABlankLine_AreWrittenAsTheyAre()
    {
        var edit = LineEdits.InsertLinesAfterDeclarations(Declarations(blankLine: true), "Private m As Long");

        Assert.AreEqual(new Range(2, 0, 2, 0), edit!.Range);
        Assert.AreEqual("Private m As Long\r\n", edit.NewText);
    }

    [TestMethod]
    public void LinesAfterTheDeclarations_DirectlyBeforeAProcedure_AreFollowedByABlankLine()
    {
        var edit = LineEdits.InsertLinesAfterDeclarations(Declarations(blankLine: false), "Private m As Long");

        Assert.AreEqual(new Range(2, 0, 2, 0), edit!.Range);
        Assert.AreEqual("Private m As Long\r\n\r\n", edit.NewText);
    }

    [TestMethod]
    public void LinesAfterTheDeclarationsOfATextWithoutALineEnd_AreEachWrittenAfterALineEnd()
    {
        var edit = LineEdits.InsertLinesAfterDeclarations(Declarations(blankLine: true, withProcedure: false, endOfText: new SourcePosition(1, 17)), "Private m As Long");

        Assert.AreEqual(new Range(1, 17, 1, 17), edit!.Range);
        Assert.AreEqual("\r\nPrivate m As Long", edit.NewText);
    }

    [TestMethod]
    public void AParse_EndsItsLinesWithACarriageReturnAndALineFeed_UnlessItSaysOtherwise()
        => Assert.AreEqual("\r\n", new ModuleParseResult().LineEnding);
}
