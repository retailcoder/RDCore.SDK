using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// Where a range of a text is after edits of that text: moved by the edits before it, left where it is by the edits after it, and gone when an edit
/// writes over it.
/// </summary>
[TestClass]
public sealed class DeclarationEditsRangeAfterTests
{
    // "count" of "Private count As Long", on the second line of a module.
    private static readonly SourceRange Count = Range(1, 8, 1, 13);

    [TestMethod]
    public void AnEditAfterTheRange_LeavesItWhereItIs()
        => Assert.AreEqual(Count, DeclarationEdits.RangeAfter(Count, [Edit(Range(2, 0, 3, 0), "Private other As Long\r\n")]));

    [TestMethod]
    public void TextInsertedBeforeTheRangeOnItsLine_MovesItRight()
        => Assert.AreEqual(Range(1, 9, 1, 14), DeclarationEdits.RangeAfter(Count, [Edit(Range(1, 0, 1, 0), "x")]));

    [TestMethod]
    [DataRow("\r\n", DisplayName = "a carriage return and a line feed")]
    [DataRow("\n", DisplayName = "a line feed")]
    [DataRow("\r", DisplayName = "a carriage return")]
    public void ALineInsertedBeforeTheRange_MovesItDown(string lineEnd)
        => Assert.AreEqual(Range(2, 8, 2, 13), DeclarationEdits.RangeAfter(Count, [Edit(Range(0, 0, 0, 0), "Dim a" + lineEnd)]));

    [TestMethod]
    public void ALineRemovedBeforeTheRange_MovesItUp()
        => Assert.AreEqual(Range(0, 8, 0, 13), DeclarationEdits.RangeAfter(Count, [Edit(Range(0, 0, 1, 0), string.Empty)]));

    [TestMethod]
    public void AnEditOfItsLineThatEndsALine_MovesTheRangeToTheLineAfterIt_ByWhatFollowsTheLastLineEnd()
        => Assert.AreEqual(Range(2, 5, 2, 10), DeclarationEdits.RangeAfter(Count, [Edit(Range(1, 0, 1, 7), "Public _\r\n    ")]));

    [TestMethod]
    public void TextInsertedWhereTheRangeStarts_IsWrittenBeforeIt()
        => Assert.AreEqual(Range(1, 11, 1, 16), DeclarationEdits.RangeAfter(Count, [Edit(Range(1, 8, 1, 8), "my_")]));

    [TestMethod]
    public void TextInsertedWhereTheRangeEnds_IsWrittenAfterIt()
        => Assert.AreEqual(Count, DeclarationEdits.RangeAfter(Count, [Edit(Range(1, 13, 1, 13), "2")]));

    [TestMethod]
    public void EditsInAnyOrder_AllMoveTheRange()
        => Assert.AreEqual(Range(2, 9, 2, 14), DeclarationEdits.RangeAfter(Count,
            [Edit(Range(1, 0, 1, 0), "x"), Edit(Range(3, 0, 3, 0), "Dim b\r\n"), Edit(Range(0, 0, 0, 0), "Dim a\r\n")]));

    [TestMethod]
    [DataRow(1, 10, 1, 11, DisplayName = "an edit of part of it")]
    [DataRow(1, 0, 1, 9, DisplayName = "an edit of its start")]
    [DataRow(1, 12, 2, 0, DisplayName = "an edit of its end")]
    [DataRow(1, 10, 1, 10, DisplayName = "an insertion inside it")]
    public void AnEditThatWritesOverTheRange_LeavesNothingOfIt(int startLine, int startCharacter, int endLine, int endCharacter)
        => Assert.IsNull(DeclarationEdits.RangeAfter(Count, [Edit(Range(startLine, startCharacter, endLine, endCharacter), "y")]));

    [TestMethod]
    public void EditsThatOverlap_AreRefused()
        => Assert.ThrowsExactly<ArgumentException>(()
            => DeclarationEdits.RangeAfter(Count, [Edit(Range(0, 0, 0, 5), "a"), Edit(Range(0, 3, 0, 8), "b")]));

    private static SourceRange Range(int startLine, int startCharacter, int endLine, int endCharacter)
        => new(new SourcePosition(startLine, startCharacter), new SourcePosition(endLine, endCharacter));

    private static RefactorTextEdit Edit(SourceRange range, string newText) => new() { Range = range, NewText = newText };
}
