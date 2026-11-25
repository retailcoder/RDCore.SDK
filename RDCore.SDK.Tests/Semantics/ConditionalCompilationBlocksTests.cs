using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Semantics;
using System.Collections.Immutable;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The shape of each <c>#If</c> block (<strong>MS-VBAL §3.4</strong>): how many branches it has, and whether one of them is an <c>#Else</c>, so that a branch is
/// compiled whatever the constants are. The precompiler trivia is written here as the parser gives it.
/// </summary>
[TestClass]
public sealed class ConditionalCompilationBlocksTests
{
    private static readonly Uri Module = new("file:///c:/ws/Main.bas");

    // a second file of one module, whose lines are numbered as the first's are.
    private static readonly Uri OtherPart = new("file:///c:/ws/Main.Part2.bas");

    private static SourceLocation At(int first, int last, Uri? file = null) => new() { Uri = file ?? Module, Range = new SourceRange(first, 0, last, 0) };

    // what is written at the start of a line.
    private static SourceLocation Line(int line, Uri? file = null) => At(line, line, file);

    private static SyntaxNodeId Id() => new(Module.AbsolutePath, []);

    // the code of a branch, from one line to another, and the blocks written in it.
    private static PrecompilerTriviaNode Body(int first, int last, params SyntaxNode[] blocks) => new(Id(), At(first, last), [.. blocks], string.Empty);

    private static PrecompilerTriviaNode Body(Uri file, int first, int last) => new(Id(), At(first, last, file), [], string.Empty);

    // a block: its #If line, then the branch bodies after it; an #ElseIf or an #Else of no body has none.
    private static PrecompilerIfBlockStatementNode Block(
        int line, PrecompilerTriviaNode body, PrecompilerTriviaNode?[] elseIfs, PrecompilerTriviaNode? @else = null, bool hasElse = false, Uri? file = null)
        => new(Id(), At(line, line, file), [
            new PrecompilerInlineIfStatementNode(Id(), At(line, line, file), []),
            body,
            .. elseIfs.Select(elseIf => new PrecompilerElseIfBlockStatementNode(Id(), At(line, line, file), elseIf is null ? [] : [elseIf])),
            .. hasElse || @else is not null
                ? [new PrecompilerElseBlockStatementNode(Id(), At(line, line, file), @else is null ? [] : [@else])]
                : Array.Empty<SyntaxNode>(),
        ]);

    private static ConditionalCompilationBlocks Of(params SyntaxNode[] blocks) => ConditionalCompilationBlocks.Of([new PrecompilerTriviaNode(Id(), At(0, 99), [.. blocks], string.Empty)]);

    // the trivia of each file of a module written in two, each with the same block at the same lines: #If, the line after it, #Else, the line after that.
    private static ConditionalCompilationBlocks TwoFilesOfOneBlockEach() => ConditionalCompilationBlocks.Of([
        new PrecompilerTriviaNode(Id(), At(0, 99), [Block(1, Body(Module, 2, 2), [], Body(Module, 4, 4))], string.Empty),
        new PrecompilerTriviaNode(Id(), At(0, 99, OtherPart), [Block(1, Body(OtherPart, 2, 2), [], Body(OtherPart, 4, 4), file: OtherPart)], string.Empty),
    ]);

    private static bool All(SourceLocation _) => true;

    [TestMethod]
    public void ABlockWithAnElse_HasOne()
    {
        var blocks = Of(Block(1, Body(2, 2), [], Body(4, 4)));

        Assert.IsTrue(blocks.HasElse(0, All));
        Assert.AreEqual(2, blocks.BranchCount(0, All));
    }

    [TestMethod]
    public void ABlockOfOneBranch_HasNoElse()
    {
        var blocks = Of(Block(1, Body(2, 2), []));

        Assert.IsFalse(blocks.HasElse(0, All));
        Assert.AreEqual(1, blocks.BranchCount(0, All));
    }

    [TestMethod]
    public void ABlockOfElseIfs_HasNoElse()
    {
        var blocks = Of(Block(1, Body(2, 2), [Body(4, 4), Body(6, 6)]));

        Assert.IsFalse(blocks.HasElse(0, All));
        Assert.AreEqual(3, blocks.BranchCount(0, All));
    }

    // an empty branch is one: none of the others is compiled when it is, though it holds no position.
    [TestMethod]
    public void AnEmptyElse_IsABranch()
    {
        var blocks = Of(Block(1, Body(2, 2), [], hasElse: true));

        Assert.IsTrue(blocks.HasElse(0, All));
        Assert.AreEqual(2, blocks.BranchCount(0, All));
        Assert.HasCount(1, blocks.BranchesAt(Line(2), All));
    }

    // a block is counted in the order it is written, the outer before the one written in it, as BranchesAt counts them.
    [TestMethod]
    public void ANestedBlock_IsCountedAfterTheOneItIsWrittenIn()
    {
        var blocks = Of(Block(1, Body(2, 8, Block(3, Body(4, 4), [], Body(6, 6))), []));

        Assert.IsFalse(blocks.HasElse(0, All));
        Assert.IsTrue(blocks.HasElse(1, All));
        CollectionAssert.AreEqual(new[] { (0, 0), (1, 1) }, blocks.BranchesAt(Line(6), All).ToArray());
    }

    [TestMethod]
    public void ABlockThatIsNotCounted_DoesNotCountInTheOrdinalOfThoseAfter()
    {
        var blocks = Of(Block(1, Body(2, 2), []), Block(4, Body(5, 5), [], Body(7, 7)));

        Assert.IsTrue(blocks.HasElse(0, block => block.Range.Start.Line > 3));
    }

    [TestMethod]
    public void AnOrdinalOfNoBlock_IsOutOfRange()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Of(Block(1, Body(2, 2), [])).HasElse(1, All));

    // a module written in two files repeats positions: each position is in the branches of its own file's blocks, and in none of the other's.
    [TestMethod]
    public void ThePositionsOfTwoFiles_AreInTheBranchesOfTheirOwnFile()
    {
        var blocks = TwoFilesOfOneBlockEach();

        Assert.IsTrue(blocks.AreAlternatives(Line(2), Line(4)), "the #If and the #Else of one file are alternatives");
        Assert.IsFalse(blocks.AreAlternatives(Line(2), Line(4, OtherPart)), "the #If of one file and the #Else of the other are compiled together");
        CollectionAssert.AreEqual(new[] { (1, 1) }, blocks.BranchesAt(Line(4, OtherPart), All).ToArray());
    }

    [TestMethod]
    public void APositionOfAnotherFile_IsInNoBranch()
    {
        var blocks = Of(Block(1, Body(2, 2), [], Body(4, 4)));

        Assert.IsTrue(blocks.IsConditional(Line(2)));
        Assert.IsFalse(blocks.IsConditional(Line(2, OtherPart)));
        Assert.IsEmpty(blocks.BranchesAt(Line(2, OtherPart), All));
    }

    // a block is counted by where it is written, its file included.
    [TestMethod]
    public void ABlockIsCounted_ByItsFileAndItsRange()
    {
        var blocks = TwoFilesOfOneBlockEach();

        Assert.IsEmpty(blocks.BranchesAt(Line(2), block => block.Uri == OtherPart));
        CollectionAssert.AreEqual(new[] { (0, 0) }, blocks.BranchesAt(Line(2, OtherPart), block => block.Uri == OtherPart).ToArray());
    }
}
