using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Server.ProtocolExtensions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.Tests.Server;

/// <summary>
/// The edits of a text, made as a client makes them (LSP 3.17 §TextEdit): each placed by the text before any of them, inserts at one position in the order given,
/// any line end, and a position past its line at the end of the line.
/// </summary>
[TestClass]
public sealed class TextEditExtensionsTests
{
    private static TextEdit Edit(int startLine, int startCharacter, int endLine, int endCharacter, string text)
        => new() { Range = new Range(startLine, startCharacter, endLine, endCharacter), NewText = text };

    [TestMethod]
    public void NoEdit_LeavesTheText()
        => Assert.AreEqual("Dim a\r\n", Array.Empty<TextEdit>().ApplyTo("Dim a\r\n"));

    [TestMethod]
    public void EveryEdit_IsPlacedByTheTextBeforeAnyOfThem_InAnyOrder()
    {
        TextEdit[] edits = [Edit(0, 4, 0, 5, "first"), Edit(2, 0, 3, 0, string.Empty), Edit(1, 0, 1, 0, "    ")];

        Assert.AreEqual("Dim first\r\n    Dim b\r\nDim d\r\n", edits.ApplyTo("Dim a\r\nDim b\r\nDim c\r\nDim d\r\n"));
        Assert.AreEqual("Dim first\r\n    Dim b\r\nDim d\r\n", edits.Reverse().ApplyTo("Dim a\r\nDim b\r\nDim c\r\nDim d\r\n"));
    }

    [TestMethod]
    public void InsertsAtOnePosition_AreWrittenInTheOrderGiven()
        => Assert.AreEqual("Public Const A = 1", new[] { Edit(0, 0, 0, 0, "Public "), Edit(0, 0, 0, 0, "Const ") }.ApplyTo("A = 1"));

    [TestMethod]
    [DataRow("a\r\nb\r\nc", DisplayName = "CRLF")]
    [DataRow("a\nb\nc", DisplayName = "LF")]
    [DataRow("a\rb\rc", DisplayName = "CR")]
    public void ALine_EndsAtAnyLineEnd(string text)
        => Assert.AreEqual(text.Replace("c", "z"), new[] { Edit(2, 0, 2, 1, "z") }.ApplyTo(text));

    [TestMethod]
    public void APositionPastItsLine_IsTheEndOfTheLine_BeforeItsLineEnd()
        => Assert.AreEqual("Dim a ' end\r\nDim b", new[] { Edit(0, 80, 0, 80, " ' end") }.ApplyTo("Dim a\r\nDim b"));

    [TestMethod]
    public void ALinePastTheLastOne_IsTheEndOfTheText()
        => Assert.AreEqual("Dim a\r\nDim b\r\n", new[] { Edit(5, 0, 5, 0, "Dim b\r\n") }.ApplyTo("Dim a\r\n"));

    [TestMethod]
    public void OverlappingEdits_AreRefused()
        => Assert.ThrowsExactly<ArgumentException>(() => new[] { Edit(0, 0, 0, 4, string.Empty), Edit(0, 2, 0, 5, "x") }.ApplyTo("Dim a"));

    [TestMethod]
    public void AnEditEndingBeforeItStarts_IsRefused()
        => Assert.ThrowsExactly<ArgumentException>(() => new[] { Edit(0, 4, 0, 2, "x") }.ApplyTo("Dim a"));

    [TestMethod]
    public void NoText_IsRefused()
        => Assert.ThrowsExactly<ArgumentNullException>(() => new[] { Edit(0, 0, 0, 0, "x") }.ApplyTo(null!));
}
