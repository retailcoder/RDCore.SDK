using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;

namespace RDCore.Tests.Model.AST;

/// <summary>
/// The nodes a node is made of, walked in the order they are written: the array a <c>ReDim</c> resizes, before its bounds; the range clauses of a
/// <c>Case</c>, before the statements of its block.
/// </summary>
[TestClass]
public sealed class SyntaxNodeWalkTests
{
    private static readonly SourceLocation Location = new(new Uri("file:///m.bas"), SourceRange.Empty);

    private static SyntaxNodeId Id() => new("m", []);

    [TestMethod]
    public void TheArrayAReDimResizes_IsWalked_BeforeItsBounds()
    {
        var target = new SimpleNameExpressionNode(Id(), Location, "items");
        var bounds = new RedimBoundsNode(Id(), Location, []);
        var redim = new RedimDeclarationNode(Id(), Location, target, [bounds]);

        var children = redim.ChildNodes().ToList();
        var descendants = redim.Descendants().ToList();

        CollectionAssert.AreEqual(new SyntaxNode[] { target, bounds }, children);
        CollectionAssert.AreEqual(new SyntaxNode[] { target, bounds }, descendants);
    }

    // the range clauses of a Case are its children, each walked once, before the statements of its block, and the values of each as what it is made of.
    [TestMethod]
    public void TheRangeClausesOfACase_AreItsChildren_WalkedOnce_BeforeItsBlock()
    {
        var low = new SimpleNameExpressionNode(Id(), Location, "low");
        var high = new SimpleNameExpressionNode(Id(), Location, "high");
        var value = new SimpleNameExpressionNode(Id(), Location, "value");
        var range = new CaseToRangeClauseNode(Id(), Location, low, high);
        var single = new CaseValueRangeClauseNode(Id(), Location, value);
        var statement = new SimpleNameExpressionNode(Id(), Location, "body");
        var caseBlock = new CaseExpressionStatementNode(Id(), Location, [range, single], new StatementBlock([statement]));

        CollectionAssert.AreEqual(new SyntaxNode[] { range, single }, caseBlock.Children.ToList());
        CollectionAssert.AreEqual(new SyntaxNode[] { range, single, statement }, caseBlock.ChildNodes().ToList());
        CollectionAssert.AreEqual(new SyntaxNode[] { range, low, high, single, value, statement }, caseBlock.Descendants().ToList());
    }
}
