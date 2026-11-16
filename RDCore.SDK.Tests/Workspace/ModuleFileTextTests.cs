using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Workspace;
using System.IO.Abstractions.TestingHelpers;
using System.Text;

namespace RDCore.SDK.Tests.Workspace;

/// <summary>
/// A module file read in the encoding it is written in and written back in it: byte for byte where it is not edited (its byte order mark or its absence, mixed
/// line endings, a newline at the end or none), an edit's line breaks written with its line's ending, and a text the encoding cannot write refused.
/// </summary>
[TestClass]
public sealed class ModuleFileTextTests
{
    // mixed line endings, an accented letter, and no newline at the end.
    private const string Text = "Attribute VB_Name = \"Module1\"\r\nPublic Sub Café()\n    Debug.Print \"é\"\rEnd Sub";

    private static byte[] BytesOf(string encoding, string text) => encoding switch
    {
        "utf-8" => new UTF8Encoding(false).GetBytes(text),
        "utf-8 bom" => [0xEF, 0xBB, 0xBF, .. new UTF8Encoding(false).GetBytes(text)],
        "windows-1252" => CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetBytes(text),
        "utf-16le bom" => [0xFF, 0xFE, .. new UnicodeEncoding(false, false).GetBytes(text)],
        _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
    };

    private static RefactorTextEdit Replace(int line, int from, int to, string with) => new() { Range = new SourceRange(line, from, line, to), NewText = with };

    [TestMethod]
    [DataRow("utf-8", "utf-8", false)]
    [DataRow("utf-8 bom", "utf-8", true)]
    [DataRow("windows-1252", "windows-1252", false)]
    [DataRow("utf-16le bom", "utf-16", true)]
    public void AnUneditedFile_ReadsAsItsText_AndIsWrittenBackByteForByte(string encoding, string webName, bool byteOrderMark)
    {
        foreach (var text in new[] { Text, Text + "\r\n" })
        {
            var bytes = BytesOf(encoding, text);

            var file = ModuleFileText.Decode(bytes, ModuleFileText.DefaultAnsiCodePage);

            Assert.AreEqual(text, file.Text);
            Assert.AreEqual(webName, file.Encoding.WebName);
            Assert.AreEqual(byteOrderMark, file.HasByteOrderMark);
            Assert.IsTrue(file.IsLossless);
            CollectionAssert.AreEqual(bytes, file.Encode());
        }
    }

    [TestMethod]
    [DataRow("utf-8")]
    [DataRow("utf-8 bom")]
    [DataRow("windows-1252")]
    [DataRow("utf-16le bom")]
    public void AnEditedFile_KeepsItsEncoding_ItsByteOrderMark_AndEveryByteNotEdited(string encoding)
    {
        var file = ModuleFileText.Decode(BytesOf(encoding, Text), ModuleFileText.DefaultAnsiCodePage);

        // "Café" on the second line becomes "Thé".
        var edited = file.Edit([Replace(1, 11, 15, "Thé")]);

        CollectionAssert.AreEqual(BytesOf(encoding, Text.Replace("Café", "Thé")), edited.Encode());
    }

    [TestMethod]
    [DataRow("a\nb\r\nc", 1, "x\ny\rz", "a\nbx\r\ny\r\nz\r\nc")]
    [DataRow("a\r\nb\nc", 2, "x\r\ny", "a\r\nb\ncx\ny")]
    [DataRow("abc", 0, "x\ny", "abcx\r\ny")]
    [DataRow("a\rb", 0, "x\ny", "ax\ry\rb")]
    public void TheLineBreaksAnEditWrites_HaveTheEndingOfItsLine(string text, int line, string written, string expected)
    {
        var file = ModuleFileText.Decode(new UTF8Encoding(false).GetBytes(text), ModuleFileText.DefaultAnsiCodePage);
        var end = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None)[line].Length;

        Assert.AreEqual(expected, file.Edit([Replace(line, end, end, written)]).Text);
    }

    [TestMethod]
    public void ATextTheEncodingCannotWrite_IsRefused_NeverWrittenWithAQuestionMark()
    {
        var file = ModuleFileText.Decode(BytesOf("windows-1252", Text), ModuleFileText.DefaultAnsiCodePage);

        var refused = Assert.ThrowsExactly<ModuleFileTextException>(() => file.Edit([Replace(2, 4, 4, "' → ")]));

        Assert.AreEqual(ModuleFileTextFailure.NotRepresentable, refused.Failure);
        StringAssert.Contains(refused.Message, "→");
        StringAssert.Contains(refused.Message, "3");
    }

    [TestMethod]
    public void AFileWhoseBytesItsByteOrderMarkDoesNotDecode_IsRead_WrittenBackAsItWas_AndNotEdited()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, 0x41, 0xE9, 0x42];

        var file = ModuleFileText.Decode(bytes, ModuleFileText.DefaultAnsiCodePage);

        Assert.IsFalse(file.IsLossless);
        CollectionAssert.AreEqual(bytes, file.Encode());
        Assert.AreEqual(ModuleFileTextFailure.NotLossless, Assert.ThrowsExactly<ModuleFileTextException>(() => file.Edit([Replace(0, 0, 0, "x")])).Failure);
    }

    [TestMethod]
    public void AFileThatIsNotUtf8_IsReadInTheCodePageTheCallerNames()
    {
        // 0xE9 is "é" in Windows-1252 and "й" in Windows-1251; alone, it is not valid UTF-8.
        byte[] bytes = [0x41, 0xE9];

        Assert.AreEqual("Aé", ModuleFileText.Decode(bytes, ModuleFileText.DefaultAnsiCodePage).Text);
        Assert.AreEqual("Aй", ModuleFileText.Decode(bytes, 1251).Text);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ModuleFileText.Decode(bytes, 4));
    }

    [TestMethod]
    public async Task AFile_IsWrittenWhole_AndLeavesNothingBesideIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rdcore-module-file-text");
        var path = Path.Combine(folder, "Module1.bas");
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { [path] = new(BytesOf("windows-1252", Text)) });
        var file = await ModuleFileText.ReadAsync(fileSystem, path, ModuleFileText.DefaultAnsiCodePage, CancellationToken.None);

        await file.Edit([Replace(1, 11, 15, "Thé")]).WriteAsync(fileSystem, path, CancellationToken.None);

        CollectionAssert.AreEqual(BytesOf("windows-1252", Text.Replace("Café", "Thé")), fileSystem.File.ReadAllBytes(path));
        CollectionAssert.AreEqual(new[] { path }, fileSystem.Directory.GetFiles(folder));
    }
}
