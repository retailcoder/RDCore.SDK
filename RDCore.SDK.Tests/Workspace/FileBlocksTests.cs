using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The modules a file declares in blocks besides its own: a <c>Class</c> or a <c>Module</c> header alone on its line, and the first end of its kind after it.
/// What is outside every block is the file's own module, and an attribute inside a block is not the file's module's name.
/// </summary>
[TestClass]
public sealed class FileBlocksTests
{
    private static string Source(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    [TestMethod]
    public void EachBlock_IsAModuleOfItsKind_NamedByItsHeader_OnTheLinesItSpans()
    {
        var blocks = FileBlocks.Find(Source(
            "Log \"start\"",
            "Class Logger ' the script's logger",
            "Public Sub Write(text)",
            "End Sub",
            "End Class",
            "module Helpers",
            "Public Const Answer = 42",
            "end module"));

        CollectionAssert.AreEqual(
            new[] { new FileBlock("Logger", FileBlockKind.Class, 1, 4), new FileBlock("Helpers", FileBlockKind.Module, 5, 7) },
            blocks.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "Class Logger", "Public Sub Write(text)", "End Sub" }, DisplayName = "a header no end follows")]
    [DataRow(new[] { "Class Logger : x = 1", "End Class" }, DisplayName = "a header with a statement after it")]
    [DataRow(new[] { "x = Add(1, _", "Module Helpers", "End Module" }, DisplayName = "a header that continues the line before it")]
    [DataRow(new[] { "Class Logger", "End Module" }, DisplayName = "an end of the other kind")]
    [DataRow(new[] { "Option Explicit", "Public Sub Main()", "End Sub" }, DisplayName = "a module that writes no block")]
    public void ALineThatIsNoHeader_StartsNoBlock(string[] lines)
        => Assert.AreEqual(0, FileBlocks.Find(Source(lines)).Count);

    [TestMethod]
    public void ABlock_EndsAtTheFirstEndOfItsKind_AHeaderInsideItIsItsCode()
        => CollectionAssert.AreEqual(new[] { new FileBlock("Outer", FileBlockKind.Class, 0, 2) },
            FileBlocks.Find(Source("Class Outer", "Class Inner", "End Class", "End Class")).ToArray());

    [TestMethod]
    [DataRow("Class Logger\r\nAttribute VB_Name = \"Other\"\r\nEnd Class\r\n", null, DisplayName = "an attribute inside a block only")]
    [DataRow("Class Logger\r\nAttribute VB_Name = \"Other\"\r\nEnd Class\r\nAttribute VB_Name = \"Backup\"\r\n", "Backup",
        DisplayName = "an attribute inside a block, and one after it")]
    public void AnAttributeInsideABlock_IsNotTheFilesModuleName(string source, string? expected)
        => Assert.AreEqual(expected, ModuleName.FromSource(source));
}
