using RDCore.SDK.Model.Source;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// The lines of a text, for edits made of it: the indent of a line, and the range that removes whole lines and leaves no blank line.
/// </summary>
[TestClass]
public sealed class DeclarationEditsLinesTests
{
    private const string Text = "Option Explicit\r\n    Private a As Long\r\n\tPrivate b As Long\r\nPublic Sub Main()";

    [TestMethod]
    [DataRow(0, "", DisplayName = "a line that starts with no blank")]
    [DataRow(1, "    ", DisplayName = "spaces")]
    [DataRow(2, "\t", DisplayName = "a tab")]
    public void TheIndentOfALine_IsTheBlanksItStartsWith(int line, string indent)
        => Assert.AreEqual(indent, DeclarationEdits.IndentOf(Text, line));

    // the white space .NET knows and the parser's lexer does not read as a blank: a no-break space, a form feed, a vertical tab, the ideographic space.
    [TestMethod]
    [DataRow("\u00A0Dim x", "", DisplayName = "a no-break space")]
    [DataRow("\fDim x", "", DisplayName = "a form feed")]
    [DataRow("\vDim x", "", DisplayName = "a vertical tab")]
    [DataRow(" \t\u3000Dim x", " \t", DisplayName = "an ideographic space after a space and a tab")]
    public void TheIndentOfALine_IsOnlyItsSpacesAndTabs(string line, string indent)
        => Assert.AreEqual(indent, DeclarationEdits.IndentOf($"Option Explicit\r\n{line}", 1));

    [TestMethod]
    [DataRow("", true, DisplayName = "an empty line")]
    [DataRow(" \t ", true, DisplayName = "spaces and a tab")]
    [DataRow("\u00A0", false, DisplayName = "a no-break space")]
    [DataRow("\f", false, DisplayName = "a form feed")]
    [DataRow("\u3000", false, DisplayName = "an ideographic space")]
    public void ALine_IsBlank_WhenItHasNothingButSpacesAndTabs(string line, bool blank)
    {
        var lines = TextLines.Of($"Option Explicit\r\n{line}x");

        Assert.AreEqual(blank, lines.IsBlankBefore(new SourcePosition(1, line.Length)), "before what is written on the line");
        Assert.AreEqual(blank, TextLines.Of($"Option Explicit\r\n{line}\r\nDim x").IsBlank(1), "the whole line");
        Assert.AreEqual(blank, TextLines.Of($"Option Explicit\r\nx{line}\r\nDim x").IsBlankAfter(new SourcePosition(1, 1)), "after what is written");
    }

    [TestMethod]
    public void WholeLines_EndAtTheStartOfTheLineAfterThem()
        => Assert.AreEqual(new SourceRange(new SourcePosition(1, 0), new SourcePosition(3, 0)), DeclarationEdits.WholeLines(Text, 1, 2));

    [TestMethod]
    public void TheLastLineOfATextThatDoesNotEndIt_IsRemovedWithTheLineEndBeforeIt()
        => Assert.AreEqual(new SourceRange(new SourcePosition(2, 18), new SourcePosition(3, 17)), DeclarationEdits.WholeLines(Text, 3, 3));

    [TestMethod]
    public void AllTheLines_AreAllTheText()
        => Assert.AreEqual(new SourceRange(SourcePosition.Zero, new SourcePosition(3, 17)), DeclarationEdits.WholeLines(Text, 0, 3));

    [TestMethod]
    public void ALineThatIsNotOneOfTheText_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DeclarationEdits.IndentOf(Text, 4));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DeclarationEdits.WholeLines(Text, 2, 1));
    }
}
