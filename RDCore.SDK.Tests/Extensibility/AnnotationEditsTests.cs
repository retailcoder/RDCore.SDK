using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Server.ProtocolExtensions;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The comment an annotation is written in, its text, and the edits that remove it or move it onto a line of its own, placed by the parse of a module.
/// </summary>
/// <remarks>
/// The parse is of this text, built as the parser builds it: each annotation inside the comment that holds it.
/// <code>
/// 0 '@ModuleDescription("Shapes")
/// 1 '@Folder("Tools") @IgnoreModule RDC00315
/// 2 Sub Main()
/// 3 End Sub
/// </code>
/// </remarks>
[TestClass]
public sealed class AnnotationEditsTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");
    private static int _next;

    private const string Text = "'@ModuleDescription(\"Shapes\")\r\n'@Folder(\"Tools\") @IgnoreModule RDC00315\r\nSub Main()\r\nEnd Sub\r\n";

    private static SyntaxNodeId Id() => new(Module.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation At(int startLine, int startCharacter, int endLine, int endCharacter)
        => new() { Uri = Module, Range = new SourceRange(startLine, startCharacter, endLine, endCharacter) };

    private static string LineOf(int line) => Text.Split("\r\n")[line];

    private static readonly CommentTriviaNode Description = new(Id(), At(0, 0, 0, LineOf(0).Length), LineOf(0));
    private static readonly CommentTriviaNode Tools = new(Id(), At(1, 0, 1, LineOf(1).Length), LineOf(1));

    private static readonly AnnotationTriviaNode ModuleDescription = new(Id(), At(0, 1, 0, LineOf(0).Length), "ModuleDescription",
        [new LiteralExpressionNode(Id(), At(0, 19, 0, 29), new VBStringValue("Shapes"))]);
    private static readonly AnnotationTriviaNode Folder = new(Id(), At(1, 1, 1, 17), "Folder",
        [new LiteralExpressionNode(Id(), At(1, 8, 1, 17), new VBStringValue("Tools"))]);
    private static readonly AnnotationTriviaNode IgnoreModule = new(Id(), At(1, 18, 1, LineOf(1).Length), "IgnoreModule",
        [new SimpleNameExpressionNode(Id(), At(1, 32, 1, 40), "RDC00315")]);

    private static readonly MemberDeclarationNode Main = new(Id(), At(2, 0, 3, 7), [], "Main", MemberKind.Procedure);

    private static readonly ModuleParseResult Parse = new()
    {
        SyntaxTree = new ModuleNode(Id(), At(0, 0, 4, 0), [Main]),
        Trivia = [Description, ModuleDescription, Tools, Folder, IgnoreModule],
        LineEnding = "\r\n",
    };

    [TestMethod]
    public void AnAnnotation_IsInTheCommentThatHoldsIt_AndIsWrittenFromItsAt()
    {
        Assert.AreSame(Tools, AnnotationEdits.CommentOf(Parse, IgnoreModule));
        Assert.AreEqual("@IgnoreModule RDC00315", AnnotationEdits.TextOf(Parse, IgnoreModule));
        Assert.AreEqual("@Folder(\"Tools\")", AnnotationEdits.TextOf(Parse, Folder));
    }

    // a line continuation carries the annotation onto the next line: it has no one-line comment, and no text.
    [TestMethod]
    public void AnAnnotationThatRunsOnPastItsLine_HasNoComment()
    {
        var onTwoLines = new AnnotationTriviaNode(Id(), At(0, 1, 1, 5), "ModuleDescription", []);
        var parse = Parse with { Trivia = [new CommentTriviaNode(Id(), At(0, 0, 1, 10), "'@ModuleDescription _"), onTwoLines] };

        Assert.IsNull(AnnotationEdits.CommentOf(parse, onTwoLines));
        Assert.IsNull(AnnotationEdits.TextOf(parse, onTwoLines));
        Assert.IsNull(AnnotationEdits.MoveAbove(parse, onTwoLines, Main.SourceLocation.Range));
    }

    [TestMethod]
    public void RemovingTheOneAnnotationOfAComment_RemovesTheCommentsLine()
        => Assert.AreEqual(LineOf(1) + "\r\nSub Main()\r\nEnd Sub\r\n",
            new[] { SyntaxEdits.Remove(AnnotationEdits.RemovalOf(Parse, ModuleDescription)) }.ApplyTo(Text));

    [TestMethod]
    public void RemovingAnAnnotationBesideAnother_LeavesTheOther()
        => Assert.AreEqual(new SourceRange(1, 18, 1, LineOf(1).Length), AnnotationEdits.RemovalOf(Parse, IgnoreModule));

    // code before the comment on its line: the line stays, and only the annotation goes.
    [TestMethod]
    public void RemovingTheAnnotationOfACommentAfterCode_LeavesTheCode()
    {
        var beep = new KeywordStatementNode(Id(), At(2, 11, 2, 15), "Beep", []);
        var comment = new CommentTriviaNode(Id(), At(2, 16, 2, 25), "'@Ignore X");
        var ignore = new AnnotationTriviaNode(Id(), At(2, 17, 2, 25), "Ignore", []);
        var parse = Parse with
        {
            SyntaxTree = new ModuleNode(Id(), At(0, 0, 4, 0), [Main with { Children = [beep] }]),
            Trivia = [comment, ignore],
        };

        Assert.AreEqual(ignore.SourceLocation.Range, AnnotationEdits.RemovalOf(parse, ignore));
    }

    [TestMethod]
    public void MovingAnAnnotationAbove_WritesItOnALineOfItsOwn_UnderTheNameGiven_AndRemovesItWhereItWas()
    {
        var edits = AnnotationEdits.MoveAbove(Parse, ModuleDescription, Main.SourceLocation.Range, "Description");

        Assert.IsNotNull(edits);
        Assert.AreEqual(LineOf(1) + "\r\n'@Description(\"Shapes\")\r\nSub Main()\r\nEnd Sub\r\n", edits.ApplyTo(Text));
    }

    [TestMethod]
    public void MovingAnAnnotationAbove_KeepsItsNameWhenNoneIsGiven_AndItsArgumentsAsWritten()
    {
        var edits = AnnotationEdits.MoveAbove(Parse, IgnoreModule, Main.SourceLocation.Range);

        Assert.IsNotNull(edits);
        Assert.AreEqual(
            "'@ModuleDescription(\"Shapes\")\r\n'@Folder(\"Tools\") \r\n'@IgnoreModule RDC00315\r\nSub Main()\r\nEnd Sub\r\n", edits.ApplyTo(Text));
    }

    // beside another annotation of its comment, only its name changes: the other annotation and its arguments stay as written.
    [TestMethod]
    public void RenamingAnAnnotation_ReplacesItsNameAfterItsAt_AndKeepsItsArguments()
    {
        var edit = AnnotationEdits.Rename(Parse, IgnoreModule, "Ignore");

        Assert.IsNotNull(edit);
        Assert.AreEqual(Text.Replace("@IgnoreModule RDC00315", "@Ignore RDC00315", StringComparison.Ordinal), new[] { edit }.ApplyTo(Text));
    }

    // a range that does not start at the annotation's own name: nothing is renamed rather than the wrong characters.
    [TestMethod]
    public void AnAnnotationWhoseTextIsNotItsName_IsNotRenamed()
    {
        var misplaced = IgnoreModule with { SourceLocation = At(1, 17, 1, LineOf(1).Length - 1) };
        var parse = Parse with { Trivia = [Tools, misplaced] };

        Assert.IsNull(AnnotationEdits.Rename(parse, misplaced, "Ignore"));
    }
}
