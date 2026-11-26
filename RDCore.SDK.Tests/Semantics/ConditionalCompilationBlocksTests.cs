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

    // a block: its #If line, then the branch bodies after it; an #If, an #ElseIf or an #Else of no body has none.
    private static PrecompilerIfBlockStatementNode Block(
        int line, PrecompilerTriviaNode? body, PrecompilerTriviaNode?[] elseIfs, PrecompilerTriviaNode? @else = null, bool hasElse = false, Uri? file = null)
        => new(Id(), At(line, line, file), [
            new PrecompilerInlineIfStatementNode(Id(), At(line, line, file), []),
            .. body is null ? Array.Empty<SyntaxNode>() : [body],
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

    // a block whose #If branch, lines 2 to 8, holds another block (#If, line 4, #Else, line 6); its own #Else is line 10. Lines move by the offset given.
    private static ConditionalCompilationBlocks Nested(int offset = 0)
        => Of(Block(1 + offset, Body(2 + offset, 8 + offset, Block(3 + offset, Body(4 + offset, 4 + offset), [], Body(6 + offset, 6 + offset))), [],
            Body(10 + offset, 10 + offset)));

    // MS-VBAL §3.4: nothing in a dead branch is part of the module, so a branch nested in one is dead whichever of its own branches would be live.
    [TestMethod]
    public void ABranchNestedInADeadBranch_IsDead()
    {
        var blocks = Nested();

        var dead = blocks.DeadOrdinals([At(2, 8)]);
        var isDead = blocks.DeadTest(dead);

        CollectionAssert.AreEqual(new[] { (0, 0) }, dead.ToArray(), "the outer #If branch, not the nested one it starts before");
        Assert.IsTrue(isDead(Line(4)), "the nested #If branch");
        Assert.IsTrue(isDead(Line(6)), "the nested #Else branch");
        Assert.IsFalse(isDead(Line(10)), "the outer #Else branch");
    }

    [TestMethod]
    public void ALiveBranchNestedInALiveBranch_IsNotDead()
    {
        var blocks = Nested();

        var dead = blocks.DeadOrdinals([At(6, 6), At(10, 10)]);
        var isDead = blocks.DeadTest(dead);

        CollectionAssert.AreEqual(new[] { (1, 1), (0, 1) }, dead.ToArray(), "the innermost branch of each, in the order given");
        Assert.IsFalse(isDead(Line(4)), "the nested #If branch");
        Assert.IsTrue(isDead(Line(6)), "the nested #Else branch");
        Assert.IsFalse(isDead(Line(2)), "the outer #If branch, before the nested block");
    }

    // a module written in two files repeats ranges: a dead branch of one file says nothing of the same lines of the other.
    [TestMethod]
    public void ADeadBranchOfOneFile_IsNoBranchOfTheOther()
    {
        var blocks = TwoFilesOfOneBlockEach();

        var dead = blocks.DeadOrdinals([At(4, 4, OtherPart)]);
        var isDead = blocks.DeadTest(dead);

        CollectionAssert.AreEqual(new[] { (1, 1) }, dead.ToArray());
        Assert.IsTrue(isDead(Line(4, OtherPart)));
        Assert.IsFalse(isDead(Line(4)), "the same line of the first file");
    }

    [TestMethod]
    public void ALocationInNoBlock_IsNeverDead()
    {
        var blocks = Of(Block(1, Body(2, 2), [], Body(4, 4)));

        Assert.IsEmpty(blocks.DeadOrdinals([At(50, 50)]), "a location no branch holds is no branch");
        Assert.IsFalse(blocks.DeadTest(blocks.DeadOrdinals([At(2, 2)]))(Line(50)));
        Assert.IsFalse(blocks.DeadTest([])(Line(2)), "no branch is dead");
    }

    // the ordinals of one parse are the dead branches of another parse of the module whose blocks have the same shape, wherever the edit moved them.
    [TestMethod]
    public void TheDeadBranchesOfAParse_AreDeadInAnEditedParseOfTheSameShape()
    {
        var evaluated = Nested();
        var edited = Nested(offset: 10);

        var isDead = edited.DeadTest(evaluated.DeadOrdinals([At(6, 6)]));

        Assert.IsTrue(edited.HasShapeOf(evaluated));
        Assert.IsTrue(isDead(Line(16)), "the nested #Else branch, ten lines down");
        Assert.IsFalse(isDead(Line(6)), "where it was before the edit");
    }

    [TestMethod]
    public void BlocksWithAnotherBranchOrBlock_OrInAnotherFile_OrNestedElsewhere_HaveAnotherShape()
    {
        var blocks = Of(Block(1, Body(2, 2), [], Body(4, 4)), Block(6, Body(7, 7), []));

        Assert.IsTrue(blocks.HasShapeOf(Of(Block(1, Body(2, 2), [], Body(4, 4)), Block(6, Body(7, 7), []))), "the same blocks");
        Assert.IsFalse(blocks.HasShapeOf(Of(Block(1, Body(2, 2), [Body(4, 4)], Body(6, 6)), Block(8, Body(9, 9), []))), "an #ElseIf added");
        Assert.IsFalse(blocks.HasShapeOf(Of(Block(1, Body(2, 2), [], Body(4, 4)))), "a block removed");
        Assert.IsFalse(blocks.HasShapeOf(Of(Block(1, Body(2, 2), [], Body(4, 4)), Block(6, Body(7, 7), []), Block(9, Body(10, 10), []))), "a block added");
        Assert.IsFalse(blocks.HasShapeOf(ConditionalCompilationBlocks.Of([
            new PrecompilerTriviaNode(Id(), At(0, 99), [Block(1, Body(2, 2), [], Body(4, 4))], string.Empty),
            new PrecompilerTriviaNode(Id(), At(0, 99, OtherPart), [Block(6, Body(OtherPart, 7, 7), [], file: OtherPart)], string.Empty),
        ])), "the second block moved to the other file");
        Assert.IsFalse(blocks.HasShapeOf(Of(Block(1, Body(2, 2), [], Body(4, 4)), Block(6, Body(7, 7), [], hasElse: true))), "an empty #Else added");
        Assert.IsFalse(blocks.HasShapeOf(Of(Block(1, Body(2, 8, Block(6, Body(7, 7), [])), [], Body(10, 10)))),
            "the second block moved into the first one's #If branch");

        var inTheIf = Of(Block(1, Body(2, 2), [], hasElse: true));
        Assert.IsTrue(inTheIf.HasShapeOf(Of(Block(1, Body(3, 3), [], hasElse: true))), "the body moved down in the #If");
        Assert.IsFalse(inTheIf.HasShapeOf(Of(Block(1, null, [], Body(3, 3)))), "the body moved from the #If to the #Else");
    }
}
