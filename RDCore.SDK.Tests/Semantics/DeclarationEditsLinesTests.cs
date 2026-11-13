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
