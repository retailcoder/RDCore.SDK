using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.Source;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The edits of a fix, placed by the positions of a syntax tree: an insertion is an empty range, a replacement and a removal span what they change, and a
/// width is known only on one line.
/// </summary>
[TestClass]
public sealed class SyntaxEditsTests
{
    [TestMethod]
    public void AnInsertion_IsAnEmptyRange_AtItsPosition()
    {
        var edit = SyntaxEdits.Insert(new SourcePosition(3, 4), "ByRef ");

        Assert.AreEqual(new Range(3, 4, 3, 4), edit.Range);
        Assert.AreEqual("ByRef ", edit.NewText);
    }

    [TestMethod]
    public void AReplacement_SpansTheLinesOfItsRange()
    {
        var edit = SyntaxEdits.Replace(new SourceRange(1, 10, 2, 4), " ");

        Assert.AreEqual(new Range(1, 10, 2, 4), edit.Range);
        Assert.AreEqual(" ", edit.NewText);
    }

    [TestMethod]
    public void ARemoval_WritesNothing()
        => Assert.AreEqual(string.Empty, SyntaxEdits.Remove(new SourceRange(0, 0, 0, 5)).NewText);

    [TestMethod]
    public void TheRangeFollowingAPosition_IsOnItsLine()
        => Assert.AreEqual(new SourceRange(2, 4, 2, 7), SyntaxEdits.Following(new SourcePosition(2, 4), 3));

    [TestMethod]
    public void TheRangePrecedingAPosition_IsOnItsLine()
        => Assert.AreEqual(new SourceRange(5, 0, 5, 4), SyntaxEdits.Preceding(new SourcePosition(5, 4), 4));

    [TestMethod]
    public void TheLinesThroughAnother_EndAtTheStartOfTheLineAfterIt()
        => Assert.AreEqual(new SourceRange(3, 0, 5, 0), SyntaxEdits.LinesThrough(3, 4));

    [TestMethod]
    public void TheLineAfterANode_StartsAfterItsLastLine()
        => Assert.AreEqual(new SourcePosition(8, 0), SyntaxEdits.LineAfter(new SourceRange(6, 4, 7, 11)));

    [TestMethod]
    public void AFix_HoldsItsEditsUnderItsTitle()
    {
        var remove = SyntaxEdits.Remove(new SourceRange(4, 4, 4, 9));
        var insert = SyntaxEdits.Insert(new SourcePosition(1, 0), "Option Explicit\r\n");

        var fix = SyntaxEdits.Fix("Write Option Explicit", remove, insert);

        Assert.AreEqual("Write Option Explicit", fix.Title);
        CollectionAssert.AreEqual(new[] { remove, insert }, fix.Edits.ToArray());
    }

    [TestMethod]
    [DataRow(1, 3, 1, 4, 1, DisplayName = "one character")]
    [DataRow(1, 3, 1, 3, 0, DisplayName = "nothing")]
    [DataRow(1, 3, 2, 3, -1, DisplayName = "on another line")]
    public void TheWidthOfASpan_IsKnownOnOneLine(int fromLine, int fromCharacter, int toLine, int toCharacter, int width)
        => Assert.AreEqual(width, SyntaxEdits.Width(new SourcePosition(fromLine, fromCharacter), new SourcePosition(toLine, toCharacter)));
}
