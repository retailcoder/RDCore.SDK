using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// The text that edits of a text leave: each edit's range replaced by what it writes, whatever order the edits are given in.
/// </summary>
[TestClass]
public sealed class DeclarationEditsApplyToTests
{
    private const string Text = "Option Explicit\r\nPrivate count As Long\r\nPublic Sub Main()";

    [TestMethod]
    public void NoEdit_LeavesTheText()
        => Assert.AreEqual(Text, DeclarationEdits.ApplyTo(Text, []));

    [TestMethod]
    public void EditsInAnyOrder_AreAllMade()
        => Assert.AreEqual("Option Explicit\r\nPublic total As Long\r\nPublic Sub Main()\r\nEnd Sub",
            DeclarationEdits.ApplyTo(Text, [Edit(Range(2, 17, 2, 17), "\r\nEnd Sub"), Edit(Range(1, 0, 1, 7), "Public"), Edit(Range(1, 8, 1, 13), "total")]));

    [TestMethod]
    public void AnEditOfWholeLines_RemovesThem()
        => Assert.AreEqual("Option Explicit\r\nPublic Sub Main()", DeclarationEdits.ApplyTo(Text, [Edit(Range(1, 0, 2, 0), string.Empty)]));

    [TestMethod]
    public void EditsThatOverlap_AreRefused()
        => Assert.ThrowsExactly<ArgumentException>(() => DeclarationEdits.ApplyTo(Text, [Edit(Range(1, 0, 1, 9), "a"), Edit(Range(1, 8, 1, 13), "b")]));

    private static SourceRange Range(int startLine, int startCharacter, int endLine, int endCharacter)
        => new(new SourcePosition(startLine, startCharacter), new SourcePosition(endLine, endCharacter));

    private static RefactorTextEdit Edit(SourceRange range, string newText) => new() { Range = range, NewText = newText };
}
