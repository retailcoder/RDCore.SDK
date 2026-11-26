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
            new[]
            {
                new FileBlock("Logger", FileBlockKind.Class, 1, 4) { NameColumn = 6, KindColumn = 0 },
                new FileBlock("Helpers", FileBlockKind.Module, 5, 7) { NameColumn = 7, KindColumn = 0 },
            },
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
        => CollectionAssert.AreEqual(new[] { new FileBlock("Outer", FileBlockKind.Class, 0, 2) { NameColumn = 6, KindColumn = 0 } },
            FileBlocks.Find(Source("Class Outer", "Class Inner", "End Class", "End Class")).ToArray());

    // the modifiers before the kind, each at most once and in their one order, say what the header writes: a visibility, Partial, and Creatable; a
    // header that states none has none. The block's end is its kind's, as a plain block's is, and its name is where the header writes it, after them.
    [TestMethod]
    [DataRow("Class Logger", false, false, null, DisplayName = "no modifier")]
    [DataRow("Partial Class Logger ' its generated part", true, false, null, DisplayName = "Partial")]
    [DataRow("Creatable Class Logger", false, true, null, DisplayName = "Creatable")]
    [DataRow("Public Class Logger", false, false, "Public", DisplayName = "Public")]
    [DataRow("  private   class Logger", false, false, "Private", DisplayName = "Private, in any case and spacing")]
    [DataRow("Public Partial Class Logger", true, false, "Public", DisplayName = "a visibility, then Partial")]
    [DataRow("Private Creatable Class Logger", false, true, "Private", DisplayName = "a visibility, then Creatable")]
    [DataRow("Partial Creatable Class Logger", true, true, null, DisplayName = "Partial, then Creatable")]
    [DataRow("PUBLIC\tPARTIAL CREATABLE CLASS Logger", true, true, "Public", DisplayName = "all three, in any case")]
    public void TheModifiersOfAHeader_AreWhatItWrites(string header, bool isPartial, bool isCreatable, string? visibility)
    {
        var block = FileBlocks.Find(Source(header, "End Class")).Single();

        Assert.AreEqual(new FileBlock("Logger", FileBlockKind.Class, 0, 1), block with
        {
            NameColumn = 0, KindColumn = 0, VisibilityColumn = null, PartialColumn = null, CreatableColumn = null,
            IsPartial = false, IsCreatable = false, Visibility = null,
        });
        Assert.AreEqual(header.IndexOf("Logger", StringComparison.Ordinal), block.NameColumn);
        Assert.AreEqual(isPartial, block.IsPartial);
        Assert.AreEqual(isCreatable, block.IsCreatable);
        Assert.AreEqual(visibility is null ? null : Enum.Parse<RDCore.SDK.Model.AccessModifier>(visibility), block.Visibility);
    }

    // each word of a header is where the header writes it, in whatever case and spacing: the visibility, Partial and Creatable where it writes them,
    // nowhere when it does not, and the kind always.
    [TestMethod]
    [DataRow("Class Logger", null, null, null, 0, DisplayName = "no modifier")]
    [DataRow("Partial Class Logger ' its generated part", null, 0, null, 8, DisplayName = "Partial")]
    [DataRow("Creatable Class Logger", null, null, 0, 10, DisplayName = "Creatable")]
    [DataRow("Public Class Logger", 0, null, null, 7, DisplayName = "Public")]
    [DataRow("  private   class Logger", 2, null, null, 12, DisplayName = "Private, in any case and spacing")]
    [DataRow("Public Partial Class Logger", 0, 7, null, 15, DisplayName = "a visibility, then Partial")]
    [DataRow("Private Creatable Class Logger", 0, null, 8, 18, DisplayName = "a visibility, then Creatable")]
    [DataRow("Partial Creatable Class Logger", null, 0, 8, 18, DisplayName = "Partial, then Creatable")]
    [DataRow("PUBLIC\tPARTIAL CREATABLE CLASS Logger", 0, 7, 15, 25, DisplayName = "all three, in any case")]
    public void EachWordOfAHeader_IsWhereItIsWritten(string header, int? visibilityColumn, int? partialColumn, int? creatableColumn, int kindColumn)
    {
        var block = FileBlocks.Find(Source(header, "End Class")).Single();

        Assert.AreEqual((visibilityColumn, partialColumn, creatableColumn, kindColumn),
            (block.VisibilityColumn, block.PartialColumn, block.CreatableColumn, block.KindColumn));
        Assert.AreEqual("Class", header.Substring(block.KindColumn, "Class".Length), true, "the kind is the word at its column");
        if (block.PartialColumn is { } partial)
        {
            Assert.AreEqual("Partial", header.Substring(partial, "Partial".Length), true, "Partial is the word at its column");
        }

        if (block.CreatableColumn is { } creatable)
        {
            Assert.AreEqual("Creatable", header.Substring(creatable, "Creatable".Length), true, "Creatable is the word at its column");
        }

        if (block.VisibilityColumn is { } at)
        {
            Assert.AreEqual(block.Visibility.ToString(), header.Substring(at, block.Visibility.ToString()!.Length), true,
                "the visibility is the word at its column");
        }
    }

    [TestMethod]
    public void AModuleBlock_TakesAVisibilityAndPartial()
        => Assert.AreEqual(
            new FileBlock("Helpers", FileBlockKind.Module, 0, 1)
            {
                NameColumn = 23, KindColumn = 16, VisibilityColumn = 0, PartialColumn = 8, IsPartial = true,
                Visibility = RDCore.SDK.Model.AccessModifier.Private,
            },
            FileBlocks.Find(Source("Private Partial Module Helpers", "End Module")).Single());

    [TestMethod]
    [DataRow(new[] { "Partial Logger", "End Class" }, DisplayName = "Partial with no kind")]
    [DataRow(new[] { "Partial Partial Class Logger", "End Class" }, DisplayName = "Partial twice")]
    [DataRow(new[] { "Creatable Creatable Class Logger", "End Class" }, DisplayName = "Creatable twice")]
    [DataRow(new[] { "Class Partial Logger", "End Class" }, DisplayName = "Partial after the kind")]
    [DataRow(new[] { "Partial Public Class Logger", "End Class" }, DisplayName = "Partial, then a visibility")]
    [DataRow(new[] { "Partial Private Module Helpers", "End Module" }, DisplayName = "Partial, then a visibility, on a module")]
    [DataRow(new[] { "Creatable Partial Class Logger", "End Class" }, DisplayName = "Creatable, then Partial")]
    [DataRow(new[] { "Creatable Public Class Logger", "End Class" }, DisplayName = "Creatable, then a visibility")]
    [DataRow(new[] { "Creatable Module Helpers", "End Module" }, DisplayName = "Creatable on a module")]
    [DataRow(new[] { "Public Partial Creatable Module Helpers", "End Module" }, DisplayName = "Creatable on a module, after the others")]
    [DataRow(new[] { "Friend Class Logger", "End Class" }, DisplayName = "Friend")]
    [DataRow(new[] { "Global Module Helpers", "End Module" }, DisplayName = "Global")]
    [DataRow(new[] { "Public Public Class Logger", "End Class" }, DisplayName = "a visibility twice")]
    [DataRow(new[] { "Public Private Class Logger", "End Class" }, DisplayName = "two visibilities")]
    [DataRow(new[] { "Public Partial Public Class Logger", "End Class" }, DisplayName = "a visibility on both sides of Partial")]
    [DataRow(new[] { "Class Public Logger", "End Class" }, DisplayName = "a visibility after the kind")]
    public void AModifierThatIsNotOnceBeforeTheKind_StartsNoBlock(string[] lines)
        => Assert.AreEqual(0, FileBlocks.Find(Source(lines)).Count);

    [TestMethod]
    [DataRow("Class Logger\r\nAttribute VB_Name = \"Other\"\r\nEnd Class\r\n", null, DisplayName = "an attribute inside a block only")]
    [DataRow("Class Logger\r\nAttribute VB_Name = \"Other\"\r\nEnd Class\r\nAttribute VB_Name = \"Backup\"\r\n", "Backup",
        DisplayName = "an attribute inside a block, and one after it")]
    public void AnAttributeInsideABlock_IsNotTheFilesModuleName(string source, string? expected)
        => Assert.AreEqual(expected, ModuleName.FromSource(source));
}
