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

    private static SourceLocation At(int first, int last) => new() { Uri = Module, Range = new SourceRange(first, 0, last, 0) };

    private static SyntaxNodeId Id() => new(Module.AbsolutePath, []);

    // the code of a branch, from one line to another, and the blocks written in it.
    private static PrecompilerTriviaNode Body(int first, int last, params SyntaxNode[] blocks) => new(Id(), At(first, last), [.. blocks], string.Empty);

    // a block: its #If line, then the branch bodies after it; an #ElseIf or an #Else of no body has none.
    private static PrecompilerIfBlockStatementNode Block(int line, PrecompilerTriviaNode body, PrecompilerTriviaNode?[] elseIfs, PrecompilerTriviaNode? @else = null, bool hasElse = false)
        => new(Id(), At(line, line), [
            new PrecompilerInlineIfStatementNode(Id(), At(line, line), []),
            body,
            .. elseIfs.Select(elseIf => new PrecompilerElseIfBlockStatementNode(Id(), At(line, line), elseIf is null ? [] : [elseIf])),
            .. hasElse || @else is not null ? [new PrecompilerElseBlockStatementNode(Id(), At(line, line), @else is null ? [] : [@else])] : Array.Empty<SyntaxNode>(),
        ]);

    private static ConditionalCompilationBlocks Of(params SyntaxNode[] blocks) => ConditionalCompilationBlocks.Of([new PrecompilerTriviaNode(Id(), At(0, 99), [.. blocks], string.Empty)]);

    private static bool All(SourceRange _) => true;

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
        Assert.HasCount(1, blocks.BranchesAt(new SourcePosition(2, 0), All));
    }

    // a block is counted in the order it is written, the outer before the one written in it, as BranchesAt counts them.
    [TestMethod]
    public void ANestedBlock_IsCountedAfterTheOneItIsWrittenIn()
    {
        var blocks = Of(Block(1, Body(2, 8, Block(3, Body(4, 4), [], Body(6, 6))), []));

        Assert.IsFalse(blocks.HasElse(0, All));
        Assert.IsTrue(blocks.HasElse(1, All));
        CollectionAssert.AreEqual(new[] { (0, 0), (1, 1) }, blocks.BranchesAt(new SourcePosition(6, 0), All).ToArray());
    }

    [TestMethod]
    public void ABlockThatIsNotCounted_DoesNotCountInTheOrdinalOfThoseAfter()
    {
        var blocks = Of(Block(1, Body(2, 2), []), Block(4, Body(5, 5), [], Body(7, 7)));

        Assert.IsTrue(blocks.HasElse(0, range => range.Start.Line > 3));
    }

    [TestMethod]
    public void AnOrdinalOfNoBlock_IsOutOfRange()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Of(Block(1, Body(2, 2), [])).HasElse(1, All));
}
