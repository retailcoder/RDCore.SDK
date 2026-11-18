using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Client;
using RDCore.SDK.Workspace;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.SDK.Tests.Client;

/// <summary>
/// A workspace edit applied to the files on disk, whole or not at all: each file edited in its own encoding, renamed and deleted where the edit says, a change
/// that cannot be made refusing the edit before anything is touched, and a failure undoing what was done before it.
/// </summary>
[TestClass]
public sealed class WorkspaceEditFilesTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "rdcore-workspace-edit-files");
    private static readonly string Ansi = Path.Combine(Folder, "Module1.bas");
    private static readonly string Utf8 = Path.Combine(Folder, "Class1.cls");

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
    private static readonly byte[] AnsiBytes = Windows1252.GetBytes("Public Sub Café()\r\nEnd Sub\r\n");
    private static readonly byte[] Utf8Bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Public Name As String\nPublic Thé As String\n")];

    private static MockFileSystem Files() => new(new Dictionary<string, MockFileData> { [Ansi] = new(AnsiBytes), [Utf8] = new(Utf8Bytes) });

    private static WorkspaceEditFiles Applier(MockFileSystem files) => new(files, ModuleFileText.DefaultAnsiCodePage);

    private static TextDocumentEdit EditOf(string path, int? version, int line, int from, int to, string with) => new()
    {
        TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = DocumentUri.FromFileSystemPath(path), Version = version },
        Edits = new TextEditContainer(new TextEdit { Range = new Range(line, from, line, to), NewText = with }),
    };

    private static WorkspaceEdit Of(params WorkspaceEditDocumentChange[] changes) => new() { DocumentChanges = new Container<WorkspaceEditDocumentChange>(changes) };

    // a client that has opened no document.
    private static int? NoneOpen(DocumentUri uri) => null;

    private static string[] Paths(IEnumerable<string> paths) => [.. paths.Select(path => DocumentUri.FromFileSystemPath(path).ToString())];

    private static readonly string Renamed = Path.Combine(Folder, "Module2.bas");

    private static WorkspaceEditDocumentChange Rename(string from, string to, RenameFileOptions? options = null)
        => new(new RenameFile { OldUri = DocumentUri.FromFileSystemPath(from), NewUri = DocumentUri.FromFileSystemPath(to), Options = options });

    private static WorkspaceEditDocumentChange Delete(string path) => new(new DeleteFile { Uri = DocumentUri.FromFileSystemPath(path) });

    // the files there are: none set aside, none written beside one.
    private static string[] FilesIn(MockFileSystem files) => [.. files.AllFiles.Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

    [TestMethod]
    public async Task ARenamedFile_IsEditedWhereTheEditLeavesIt()
    {
        var files = Files();

        var result = await Applier(files).ApplyAsync(Of(Rename(Ansi, Renamed), EditOf(Renamed, null, 0, 11, 15, "Thé")), NoneOpen, CancellationToken.None);

        Assert.IsTrue(result.Applied, result.FailureReason);
        CollectionAssert.AreEqual(Paths([Renamed]), Paths(result.Written));
        CollectionAssert.AreEqual(Windows1252.GetBytes("Public Sub Thé()\r\nEnd Sub\r\n"), files.File.ReadAllBytes(Renamed));
        CollectionAssert.AreEqual(new[] { "Class1.cls", "Module2.bas" }, FilesIn(files));
    }

    [TestMethod]
    public async Task ADeletedFile_IsGone_AndNothingIsLeftBesideIt()
    {
        var files = Files();

        var result = await Applier(files).ApplyAsync(Of(EditOf(Ansi, null, 0, 11, 15, "Thé"), Delete(Utf8)), NoneOpen, CancellationToken.None);

        Assert.IsTrue(result.Applied, result.FailureReason);
        CollectionAssert.AreEqual(new[] { "Module1.bas" }, FilesIn(files));
    }

    [TestMethod]
    [DataRow(true, false, DisplayName = "overwrite")]
    [DataRow(false, true, DisplayName = "ignore if it exists")]
    public async Task ARenameOverAFileThatExists_IsMade_OnlyAsTheEditSays(bool overwrite, bool ignoreIfExists)
    {
        var files = Files();

        var result = await Applier(files).ApplyAsync(
            Of(Rename(Utf8, Ansi, new RenameFileOptions { Overwrite = overwrite, IgnoreIfExists = ignoreIfExists })), NoneOpen, CancellationToken.None);

        Assert.IsTrue(result.Applied, result.FailureReason);
        CollectionAssert.AreEqual(overwrite ? Utf8Bytes : AnsiBytes, files.File.ReadAllBytes(Ansi));
        CollectionAssert.AreEqual(overwrite ? new[] { "Module1.bas" } : ["Class1.cls", "Module1.bas"], FilesIn(files));
    }

    [TestMethod]
    public async Task AWriteThatFails_PutsBackTheTextsWrittenBeforeIt()
    {
        var files = Files();
        files.File.SetAttributes(Utf8, FileAttributes.ReadOnly);

        var result = await Applier(files).ApplyAsync(Of(EditOf(Ansi, null, 0, 11, 15, "Thé"), EditOf(Utf8, null, 1, 7, 10, "Café")), NoneOpen, CancellationToken.None);

        Assert.IsFalse(result.Applied);
        Assert.AreEqual(1, result.FailedChange);
        Assert.AreEqual(0, result.Written.Count, "everything was put back");
        CollectionAssert.AreEqual(AnsiBytes, files.File.ReadAllBytes(Ansi));
        CollectionAssert.AreEqual(Utf8Bytes, files.File.ReadAllBytes(Utf8));
        CollectionAssert.AreEqual(new[] { "Class1.cls", "Module1.bas" }, FilesIn(files));
    }

    [TestMethod]
    public async Task ARenameThatFails_ReversesTheRenamesBeforeIt_AndGivesBackAFileDeletedBeforeIt()
    {
        var files = Files();

        var result = await Applier(files).ApplyAsync(
            Of(Delete(Utf8), Rename(Ansi, Renamed), Rename(Renamed, Path.Combine(Folder, "Missing", "Module3.bas"))), NoneOpen, CancellationToken.None);

        Assert.IsFalse(result.Applied);
        Assert.AreEqual(2, result.FailedChange);
        CollectionAssert.AreEqual(AnsiBytes, files.File.ReadAllBytes(Ansi));
        CollectionAssert.AreEqual(Utf8Bytes, files.File.ReadAllBytes(Utf8));
        CollectionAssert.AreEqual(new[] { "Class1.cls", "Module1.bas" }, FilesIn(files));
    }

    // a file is deleted for good only once every change was made: undoing the edit never loses it.
    [TestMethod]
    public async Task ADelete_IsMadeForGoodLast_AWriteThatFailsAfterItGivesTheFileBack()
    {
        var files = Files();
        files.File.SetAttributes(Ansi, FileAttributes.ReadOnly);

        var result = await Applier(files).ApplyAsync(Of(Delete(Utf8), EditOf(Ansi, null, 0, 11, 15, "Thé")), NoneOpen, CancellationToken.None);

        Assert.IsFalse(result.Applied);
        Assert.AreEqual(1, result.FailedChange);
        CollectionAssert.AreEqual(Utf8Bytes, files.File.ReadAllBytes(Utf8));
        CollectionAssert.AreEqual(new[] { "Class1.cls", "Module1.bas" }, FilesIn(files));
    }

    [TestMethod]
    public void TheCapability_IsWhatTheApplierTakes()
    {
        var capability = WorkspaceEditFiles.Capability;

        Assert.IsTrue(capability.DocumentChanges);
        CollectionAssert.AreEquivalent(new[] { ResourceOperationKind.Rename, ResourceOperationKind.Delete }, capability.ResourceOperations!.ToArray());
        Assert.AreEqual(FailureHandlingKind.Undo, capability.FailureHandling);
    }

    [TestMethod]
    public async Task AnEdit_IsWrittenToEachFile_InItsEncoding_AndSaysWhichFilesItWrote()
    {
        var files = Files();

        var result = await Applier(files).ApplyAsync(Of(
            EditOf(Ansi, null, 0, 11, 15, "Thé"),
            EditOf(Utf8, null, 1, 7, 10, "Café"),
            EditOf(Ansi, null, 1, 0, 0, "' é\r\n")), NoneOpen, CancellationToken.None);

        Assert.IsTrue(result.Applied, result.FailureReason);
        CollectionAssert.AreEqual(Paths([Ansi, Utf8]), Paths(result.Written));
        CollectionAssert.AreEqual(Windows1252.GetBytes("Public Sub Thé()\r\n' é\r\nEnd Sub\r\n"), files.File.ReadAllBytes(Ansi));
        CollectionAssert.AreEqual((byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Public Name As String\nPublic Café As String\n")], files.File.ReadAllBytes(Utf8));
        Assert.IsTrue(result.ToResponse().Applied);
    }

    [TestMethod]
    public async Task AnEditOfChanges_IsWrittenToo()
    {
        var files = Files();
        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
            {
                [DocumentUri.FromFileSystemPath(Ansi)] = [new TextEdit { Range = new Range(0, 11, 0, 15), NewText = "Thé" }],
            },
        };

        var result = await Applier(files).ApplyAsync(edit, NoneOpen, CancellationToken.None);

        Assert.IsTrue(result.Applied, result.FailureReason);
        CollectionAssert.AreEqual(Windows1252.GetBytes("Public Sub Thé()\r\nEnd Sub\r\n"), files.File.ReadAllBytes(Ansi));
    }

    [TestMethod]
    public async Task AChangeThatCannotBeMade_RefusesTheEdit_AndNoFileIsWritten()
    {
        var cases = new (string Why, WorkspaceEdit Edit)[]
        {
            ("a version the client does not have", Of(EditOf(Ansi, null, 0, 11, 15, "Thé"), EditOf(Utf8, 3, 0, 0, 0, "x"))),
            ("a file that does not exist", Of(EditOf(Ansi, null, 0, 11, 15, "Thé"), EditOf(Path.Combine(Folder, "Gone.bas"), null, 0, 0, 0, "x"))),
            ("a character Windows-1252 cannot write", Of(EditOf(Utf8, null, 0, 0, 0, "' é\n"), EditOf(Ansi, null, 0, 0, 0, "' → "))),
            ("an edit of a line the text does not have", Of(EditOf(Utf8, null, 0, 0, 0, "x"), EditOf(Ansi, null, 9, 0, 0, "x"))),
            ("a file it creates", Of(EditOf(Ansi, null, 0, 11, 15, "Thé"), new CreateFile { Uri = DocumentUri.FromFileSystemPath(Path.Combine(Folder, "New.bas")) })),
            ("a folder it deletes", Of(EditOf(Ansi, null, 0, 11, 15, "Thé"), Delete(Folder))),
            ("a file it renames over another", Of(EditOf(Ansi, null, 0, 11, 15, "Thé"), Rename(Utf8, Ansi))),
            ("a file it renames that does not exist", Of(Rename(Ansi, Renamed), Rename(Ansi, Utf8))),
        };

        foreach (var (why, edit) in cases)
        {
            var files = Files();

            var result = await Applier(files).ApplyAsync(edit, NoneOpen, CancellationToken.None);

            Assert.IsFalse(result.Applied, why);
            Assert.AreEqual(1, result.FailedChange, why);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.FailureReason), why);
            Assert.AreEqual(0, result.Written.Count, why);
            CollectionAssert.AreEqual(AnsiBytes, files.File.ReadAllBytes(Ansi), why);
            CollectionAssert.AreEqual(Utf8Bytes, files.File.ReadAllBytes(Utf8), why);
        }
    }

    [TestMethod]
    public async Task AChangeOfTheVersionTheClientHas_IsMade()
    {
        var files = Files();

        var result = await Applier(files).ApplyAsync(Of(EditOf(Ansi, 4, 0, 11, 15, "Thé")), uri => 4, CancellationToken.None);

        Assert.IsTrue(result.Applied, result.FailureReason);
    }
}
