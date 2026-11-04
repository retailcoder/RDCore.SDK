using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.Tests;

[TestClass]
public class SyntaxNodeIdTests
{
    [TestMethod]
    public void CompareTo_SortsByDocumentUri()
    {
        var node1 = new SyntaxNodeId("document1", []);
        var node2 = new SyntaxNodeId("document2", []);

        var result = node1.CompareTo(node2);

        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    [DataRow(new int[] { 0, 1 }, -1)] // same lineage, but shallower: compares less than
    [DataRow(new int[] { 1, 0 }, 1)] // next sibling; compares greater than
    [DataRow(new int[] { 0, 1, 2 }, 0)] // same lineage; compares equal
    [DataRow(new int[] { 0, 1, 2, 0 }, 1)] // same lineage, but deeper: compares greater than
    public void CompareTo_Comparisons(int[] lineage, int expected)
    {
        var node1 = new SyntaxNodeId("document", [0, 1, 2]);
        var node2 = new SyntaxNodeId("document", [..lineage]);

        var result = node2.CompareTo(node1);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void EqualsGetHashCodeCompareTo_AreCoherent()
    {
        var node1 = new SyntaxNodeId("document1", [0, 1, 2]);
        var node2 = new SyntaxNodeId("document1", [0, 1, 2]);

        var comparison = node1.CompareTo(node2);

        Assert.AreEqual(node1, node2);
        Assert.AreEqual(node1.GetHashCode(), node2.GetHashCode());
        Assert.AreEqual(0, comparison);
    }
}
