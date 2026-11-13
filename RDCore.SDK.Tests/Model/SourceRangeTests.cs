using RDCore.SDK.Model.Source;

namespace RDCore.Tests.Model;

/// <summary>
/// A range holds the positions from its start up to its end, which is not in it.
/// </summary>
[TestClass]
public sealed class SourceRangeTests
{
    [TestMethod]
    [DataRow(1, 4, true)]
    [DataRow(1, 9, true)]
    [DataRow(2, 0, true)]
    [DataRow(3, 1, true)]
    [DataRow(3, 2, false)]
    [DataRow(1, 3, false)]
    [DataRow(0, 9, false)]
    [DataRow(4, 0, false)]
    public void APosition_IsInARange_FromItsStartUpToItsEnd(int line, int character, bool expected)
        => Assert.AreEqual(expected, new SourceRange(1, 4, 3, 2).Contains(new SourcePosition(line, character)));

    [TestMethod]
    public void AnEmptyRange_HoldsNoPosition()
        => Assert.IsFalse(new SourceRange(2, 5, 2, 5).Contains(new SourcePosition(2, 5)));
}
