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

    [TestMethod]
    [DataRow(1, 4, 3, 2, true, DisplayName = "the range itself")]
    [DataRow(1, 6, 2, 0, true, DisplayName = "within it")]
    [DataRow(1, 4, 1, 4, true, DisplayName = "empty, at its start")]
    [DataRow(3, 2, 3, 2, true, DisplayName = "empty, at its end")]
    [DataRow(1, 3, 2, 0, false, DisplayName = "starting before it")]
    [DataRow(2, 0, 3, 3, false, DisplayName = "ending after it")]
    [DataRow(0, 0, 4, 0, false, DisplayName = "around it")]
    [DataRow(3, 3, 3, 3, false, DisplayName = "empty, after its end")]
    public void ARange_IsWithinAnother_FromItsStartThroughItsEnd(int startLine, int startCharacter, int endLine, int endCharacter, bool expected)
        => Assert.AreEqual(expected, new SourceRange(1, 4, 3, 2).Contains(new SourceRange(startLine, startCharacter, endLine, endCharacter)));

    [TestMethod]
    [DataRow(1, 4, 3, 2, true, DisplayName = "the range itself")]
    [DataRow(0, 0, 1, 5, true, DisplayName = "overlapping its start")]
    [DataRow(3, 1, 4, 0, true, DisplayName = "overlapping its end")]
    [DataRow(2, 0, 2, 3, true, DisplayName = "within it")]
    [DataRow(0, 0, 4, 0, true, DisplayName = "around it")]
    [DataRow(0, 0, 1, 4, false, DisplayName = "ending at its start")]
    [DataRow(3, 2, 4, 0, false, DisplayName = "starting at its end")]
    [DataRow(2, 0, 2, 0, false, DisplayName = "empty, within it")]
    public void TwoRanges_Intersect_WhenTheyShareACharacter(int startLine, int startCharacter, int endLine, int endCharacter, bool expected)
    {
        var range = new SourceRange(1, 4, 3, 2);
        var other = new SourceRange(startLine, startCharacter, endLine, endCharacter);

        Assert.AreEqual(expected, range.Intersects(other));
        Assert.AreEqual(expected, other.Intersects(range), "either way");
    }

    [TestMethod]
    [DataRow(2, 0, 2, 3, true, DisplayName = "within it")]
    [DataRow(0, 0, 1, 4, true, DisplayName = "ending at its start")]
    [DataRow(3, 2, 4, 0, true, DisplayName = "starting at its end")]
    [DataRow(2, 0, 2, 0, true, DisplayName = "empty, within it")]
    [DataRow(1, 4, 1, 4, true, DisplayName = "empty, at its start")]
    [DataRow(0, 0, 1, 3, false, DisplayName = "ending before its start")]
    [DataRow(3, 3, 3, 3, false, DisplayName = "empty, after its end")]
    public void TwoRanges_Touch_WhenNeitherEndsBeforeTheOtherStarts(int startLine, int startCharacter, int endLine, int endCharacter, bool expected)
    {
        var range = new SourceRange(1, 4, 3, 2);
        var other = new SourceRange(startLine, startCharacter, endLine, endCharacter);

        Assert.AreEqual(expected, range.Touches(other));
        Assert.AreEqual(expected, other.Touches(range), "either way");
    }
}
