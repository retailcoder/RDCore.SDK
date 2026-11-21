using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// The lines of a text, read by the positions a syntax tree gives: what is written around a position, and the text that edits leave, with where each
/// edit's text is written in it.
/// </summary>
[TestClass]
public sealed class TextLinesTests
{
    private const string Text = "Sub Main()\r\n    Add 1 : Add 2  \r\nEnd Sub";

    [TestMethod]
    [DataRow("a\r\nb\nc\rd", DisplayName = "a carriage return and a line feed, a line feed, a carriage return")]
    public void ALine_EndsAtAnyLineEnd(string text)
    {
        var lines = TextLines.Of(text);

        Assert.AreEqual(4, lines.Count);
        CollectionAssert.AreEqual(new[] { "\r\n", "\n", "\r", string.Empty }, Enumerable.Range(0, lines.Count).Select(lines.NewLineOf).ToArray());
        Assert.AreEqual("d", lines.Of(new SourceRange(new SourcePosition(3, 0), lines.End)));
    }

    [TestMethod]
    [DataRow(4, true, DisplayName = "where the statement starts")]
    [DataRow(5, false, DisplayName = "in the statement")]
    public void IsBlankBefore_IsWhetherOnlyIndentationIsWrittenBeforeThePosition(int character, bool blank)
        => Assert.AreEqual(blank, TextLines.Of(Text).IsBlankBefore(new SourcePosition(1, character)));

    [TestMethod]
    [DataRow(17, true, DisplayName = "after the last statement, its trailing spaces left")]
    [DataRow(9, false, DisplayName = "after the first statement, another following it")]
    public void IsBlankAfter_IsWhetherOnlySpaceIsWrittenAfterThePosition_UpToTheEndOfItsLine(int character, bool blank)
        => Assert.AreEqual(blank, TextLines.Of(Text).IsBlankAfter(new SourcePosition(1, character)));

    [TestMethod]
    public void IsBlankAfter_DoesNotReadTheNextLine()
        => Assert.IsTrue(TextLines.Of("Sub Main()\r\nEnd Sub").IsBlankAfter(new SourcePosition(0, 10)));

    [TestMethod]
    public void TheTextOfARange_WithEditsInIt_ReadsAsTheyLeaveIt()
        => Assert.AreEqual("Add 10 : Total",
            TextLines.Of(Text).Of(Range(1, 4, 1, 15), [Edit(Range(1, 8, 1, 9), "10"), Edit(Range(1, 12, 1, 15), "Total")]));

    [TestMethod]
    public void AnEditOutsideTheRange_IsRefused()
        => Assert.ThrowsExactly<ArgumentException>(() => TextLines.Of(Text).Of(Range(1, 4, 1, 9), [Edit(Range(1, 12, 1, 15), "Total")]));

    [TestMethod]
    public void Apply_SaysWhereEachEditsTextIsWritten_InTheOrderOfTheEdits()
    {
        // the second edit, earlier in the text, makes the first line longer: the first edit's text moves down a line.
        var edited = TextLines.Apply(Text, [Edit(Range(1, 12, 1, 15), "Total"), Edit(Range(0, 10, 0, 10), "\r\n    Dim n As Long")]);

        Assert.AreEqual("Sub Main()\r\n    Dim n As Long\r\n    Add 1 : Total 2  \r\nEnd Sub", edited.Text);
        CollectionAssert.AreEqual(new[] { Range(2, 12, 2, 17), Range(0, 10, 1, 17) }, edited.Written.ToArray());
    }

    [TestMethod]
    public void Apply_WritesInsertsAtOnePosition_InTheOrderTheyAreGiven()
        => Assert.AreEqual("Sub Main()ab\r\n    Add 1 : Add 2  \r\nEnd Sub",
            TextLines.Apply(Text, [Edit(Range(0, 10, 0, 10), "a"), Edit(Range(0, 10, 0, 10), "b")]).Text);

    [TestMethod]
    public void Apply_RefusesEditsThatOverlap()
        => Assert.ThrowsExactly<ArgumentException>(() => TextLines.Apply(Text, [Edit(Range(1, 4, 1, 9), "a"), Edit(Range(1, 8, 1, 13), "b")]));

    [TestMethod]
    public void PositionOf_IsTheOffsetsLineAndCharacter()
    {
        var lines = TextLines.Of(Text);
        var position = new SourcePosition(1, 12);

        Assert.AreEqual(position, lines.PositionOf(lines.Offset(position)));
        Assert.AreEqual(new SourcePosition(2, 0), lines.PositionOf(lines.Offset(lines.StartOfLineAfter(1))));
    }

    private static SourceRange Range(int startLine, int startCharacter, int endLine, int endCharacter)
        => new(new SourcePosition(startLine, startCharacter), new SourcePosition(endLine, endCharacter));

    private static RefactorTextEdit Edit(SourceRange range, string newText) => new() { Range = range, NewText = newText };
}
