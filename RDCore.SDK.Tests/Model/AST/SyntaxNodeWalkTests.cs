using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;

namespace RDCore.Tests.Model.AST;

/// <summary>
/// The nodes a node is made of, walked in the order they are written: the array a <c>ReDim</c> resizes, before its bounds.
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
}
