using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Client;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Server;
using RDCore.SDK.Server.ProtocolExtensions;
using RDCore.SDK.Workspace;
using System.IO.Abstractions.TestingHelpers;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.Tests.Server;

/// <summary>
/// A binary file replaced by a workspace edit: its new content staged beside it, put in its place by renames that overwrite nothing, deleted when the file
/// changes before the edit is applied, and left-behind staged files swept; and which clients take such an edit.
/// </summary>
[TestClass]
public sealed class StagedFilesTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "rdcore-staged-files");
    private static readonly string Frx = Path.Combine(Folder, "Dialog.frx");
    private static readonly string Frm = Path.Combine(Folder, "Dialog.frm");
    private static readonly DocumentUri Target = DocumentUri.FromFileSystemPath(Frx);

    private static readonly byte[] Original = [1, 2, 3, 4];
    private static readonly byte[] Content = [5, 6, 7, 8, 9];

    private static MockFileSystem Files() => new(new Dictionary<string, MockFileData>
    {
        [Frx] = new(Original),
        [Frm] = new("Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} Dialog\r\n   OleObjectBlob   =   \"Dialog.frx\":0000\r\nEnd\r\nPublic Sub Ok_Click()\r\nEnd Sub\r\n"),
    });

    private static string[] FilesIn(MockFileSystem files) => [.. files.AllFiles.Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

    // the edit that replaces the file and renames a handler with it: the file set aside, the staged one in its place, the text edited, the backup deleted.
    private static WorkspaceEdit Replacing(StagedFile staged) => new()
    {
        DocumentChanges = new Container<WorkspaceEditDocumentChange>([
            .. staged.Installing(),
            new(new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = DocumentUri.FromFileSystemPath(Frm) },
                Edits = new TextEditContainer(new TextEdit { Range = new Range(3, 11, 3, 13), NewText = "Accept" }),
            }),
            staged.Removing(),
        ]),
    };

    private static Task<WorkspaceEditFilesResult> ApplyAsync(MockFileSystem files, WorkspaceEdit edit)
        => new WorkspaceEditFiles(files, ModuleFileText.DefaultAnsiCodePage).ApplyAsync(edit, _ => null, CancellationToken.None);

    [TestMethod]
    public void Stage_WritesTheContentBesideTheFile_NamedForItsEdit()
    {
        var files = Files();
        var staged = new StagedFiles(files).Stage(Target, Original, Content);

        CollectionAssert.AreEqual(Content, files.File.ReadAllBytes(staged.Staged.GetFileSystemPath()));
        Assert.IsTrue(StagedFiles.IsStaged(staged.Staged.GetFileSystemPath()));
        StringAssert.StartsWith(Path.GetFileName(staged.Backup.GetFileSystemPath()), "Dialog.frx.");
        Assert.IsFalse(files.File.Exists(staged.Backup.GetFileSystemPath()));
        CollectionAssert.AreEqual(Original, files.File.ReadAllBytes(Frx));
    }

    [TestMethod]
    public async Task TheEdit_PutsTheContentInPlace_AndLeavesNothingBeside()
    {
        var files = Files();
        var stagedFiles = new StagedFiles(files);
        var staged = stagedFiles.Stage(Target, Original, Content);

        var result = await ApplyAsync(files, Replacing(staged));

        Assert.IsTrue(result.Applied, result.FailureReason);
        CollectionAssert.AreEqual(Content, files.File.ReadAllBytes(Frx));
        StringAssert.Contains(files.File.ReadAllText(Frm), "Public Sub Accept_Click()");
        CollectionAssert.AreEqual(new[] { "Dialog.frm", "Dialog.frx" }, FilesIn(files));
        Assert.IsTrue(stagedFiles.Verify(Target));
    }

    [TestMethod]
    public async Task AFileThatChangedAfterItWasStaged_LosesItsStagedFile_AndTheEditPutsEverythingBack()
    {
        var files = Files();
        var stagedFiles = new StagedFiles(files);
        var staged = stagedFiles.Stage(Target, Original, Content);
        files.File.WriteAllBytes(Frx, [1, 2, 3, 0]);

        Assert.IsFalse(stagedFiles.Verify(Target));
        var result = await ApplyAsync(files, Replacing(staged));

        Assert.IsFalse(result.Applied);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0 }, files.File.ReadAllBytes(Frx));
        StringAssert.Contains(files.File.ReadAllText(Frm), "Public Sub Ok_Click()");
        CollectionAssert.AreEqual(new[] { "Dialog.frm", "Dialog.frx" }, FilesIn(files));
    }

    [TestMethod]
    public void AFileOfAnotherLength_IsOneThatChanged()
    {
        var files = Files();
        var stagedFiles = new StagedFiles(files);
        var staged = stagedFiles.Stage(Target, Original, Content);
        files.File.WriteAllBytes(Frx, [1, 2, 3, 4, 5]);

        Assert.IsFalse(stagedFiles.Verify(staged.Staged));
        Assert.IsFalse(files.File.Exists(staged.Staged.GetFileSystemPath()));
    }

    [TestMethod]
    public void AFileAsItWas_KeepsItsStagedFile_AlsoOnceTheEditHasSetItAside()
    {
        var files = Files();
        var stagedFiles = new StagedFiles(files);
        var staged = stagedFiles.Stage(Target, Original, Content);

        Assert.IsTrue(stagedFiles.Verify(Target));
        files.File.Move(Frx, staged.Backup.GetFileSystemPath());
        Assert.IsTrue(stagedFiles.Verify(Target));
        Assert.IsTrue(stagedFiles.Verify(staged.Staged));
        Assert.IsTrue(files.File.Exists(staged.Staged.GetFileSystemPath()));
    }

    [TestMethod]
    public void AnotherFileThatChanged_ChangesNothing()
    {
        var files = Files();
        var stagedFiles = new StagedFiles(files);
        var staged = stagedFiles.Stage(Target, Original, Content);
        files.File.WriteAllText(Frm, "changed");

        Assert.IsTrue(stagedFiles.Verify(DocumentUri.FromFileSystemPath(Frm)));
        Assert.IsTrue(files.File.Exists(staged.Staged.GetFileSystemPath()));
    }

    [TestMethod]
    public void StagingAgain_DeletesTheFileStagedBefore()
    {
        var files = Files();
        var stagedFiles = new StagedFiles(files);
        var first = stagedFiles.Stage(Target, Original, Content);
        var second = stagedFiles.Stage(Target, Original, [9]);

        Assert.IsFalse(files.File.Exists(first.Staged.GetFileSystemPath()));
        Assert.IsTrue(files.File.Exists(second.Staged.GetFileSystemPath()));
    }

    [TestMethod]
    public void Discard_DeletesEveryStagedFile()
    {
        var files = Files();
        var stagedFiles = new StagedFiles(files);
        stagedFiles.Stage(Target, Original, Content);

        stagedFiles.Dispose();

        CollectionAssert.AreEqual(new[] { "Dialog.frm", "Dialog.frx" }, FilesIn(files));
    }

    [TestMethod]
    public void Sweep_DeletesTheStagedFilesLeftBehind_AndNothingElse()
    {
        var files = Files();
        var left = Path.Combine(Folder, "Dialog.frx.0123456789abcdef0123456789ABCDEF.rdcore");
        files.AddFile(left, new MockFileData([1]));
        files.AddFile(Path.Combine(Folder, "Dialog.frx.not-an-id.rdcore"), new MockFileData([1]));
        files.AddFile(Path.Combine(Folder, "Other.frx.0123456789abcdef0123456789abcdef.rdcore"), new MockFileData([1]));
        files.AddFile(Path.Combine(Folder, "Dialog.frx.0123456789abcdef0123456789abcdef.bak"), new MockFileData([1]));
        var stagedFiles = new StagedFiles(files);
        var waiting = stagedFiles.Stage(Target, Original, Content);

        Assert.AreEqual(1, stagedFiles.Sweep(Target));

        Assert.IsFalse(files.File.Exists(left));
        Assert.IsTrue(files.File.Exists(waiting.Staged.GetFileSystemPath()));
        Assert.AreEqual(6, files.AllFiles.Count());
    }

    [TestMethod]
    [DataRow("Dialog.frx.0123456789abcdef0123456789abcdef.rdcore", true)]
    [DataRow("DIALOG.FRX.0123456789ABCDEF0123456789ABCDEF.RDCORE", true)]
    [DataRow("Dialog.frx.0123456789abcdef0123456789abcde.rdcore", false, DisplayName = "31 digits")]
    [DataRow("Dialog.frx.0123456789abcdef0123456789abcdeg.rdcore", false, DisplayName = "not hexadecimal")]
    [DataRow("Dialog.frx.rdcore", false)]
    [DataRow(".0123456789abcdef0123456789abcdef.rdcore", false, DisplayName = "no file")]
    [DataRow("Dialog.frx", false)]
    public void IsStaged_IsAFileThenAnIdThenTheExtension(string name, bool isStaged)
        => Assert.AreEqual(isStaged, StagedFiles.IsStaged(Path.Combine(Folder, name)));

    [TestMethod]
    public void WillRename_IsAskedOfEveryStagedFile()
    {
        var pattern = StagedFiles.WillRename.Filters.Single().Pattern;

        Assert.AreEqual(("**/*.rdcore", FileOperationPatternKind.File, true), (pattern.Glob, pattern.Matches, pattern.Options?.IgnoreCase));
    }

    [TestMethod]
    public void TheWillRenameRequest_IsReadWithTheOldAndTheNewUriOfEachFile()
    {
        var request = Newtonsoft.Json.JsonConvert.DeserializeObject<WillRenameWorkspaceFilesParams>(
            """{"files":[{"oldUri":"file:///c%3A/ws/Dialog.frx.0123456789abcdef0123456789abcdef.rdcore","newUri":"file:///c%3A/ws/Dialog.frx"}]}""");

        Assert.AreEqual(("file:///c%3A/ws/Dialog.frx.0123456789abcdef0123456789abcdef.rdcore", "file:///c%3A/ws/Dialog.frx"),
            (request?.Files.Single().OldUri, request?.Files.Single().NewUri));
    }

    private static ClientCapabilities Taking(FailureHandlingKind? failure, bool documentChanges = true, params ResourceOperationKind[] resources) => new()
    {
        Workspace = new WorkspaceClientCapabilities
        {
            WorkspaceEdit = new WorkspaceEditCapability
            {
                DocumentChanges = documentChanges,
                ResourceOperations = new Container<ResourceOperationKind>(resources),
                FailureHandling = failure,
            },
        },
    };

    private static readonly ResourceOperationKind[] RenameAndDelete = [ResourceOperationKind.Rename, ResourceOperationKind.Delete];

    [TestMethod]
    public void AClientThatUndoesAFailedEdit_TakesAFileReplacement()
    {
        Assert.IsTrue(Taking(FailureHandlingKind.Undo, true, RenameAndDelete).TakesFileReplacement());
        Assert.IsTrue(Taking(FailureHandlingKind.Transactional, true, RenameAndDelete).TakesFileReplacement());
        Assert.IsTrue(new ClientCapabilities { Workspace = new WorkspaceClientCapabilities { WorkspaceEdit = WorkspaceEditFiles.Capability } }.TakesFileReplacement());
    }

    [TestMethod]
    public void AClientThatCannotPutAFileBack_DoesNotTakeAFileReplacement()
    {
        Assert.IsFalse(Taking(FailureHandlingKind.TextOnlyTransactional, true, RenameAndDelete).TakesFileReplacement());
        Assert.IsFalse(Taking(FailureHandlingKind.Abort, true, RenameAndDelete).TakesFileReplacement());
        Assert.IsFalse(Taking(null, true, RenameAndDelete).TakesFileReplacement());
        Assert.IsFalse(Taking(FailureHandlingKind.Undo, true, ResourceOperationKind.Rename).TakesFileReplacement());
        Assert.IsFalse(Taking(FailureHandlingKind.Undo, false, RenameAndDelete).TakesFileReplacement());
        Assert.IsFalse(((ClientCapabilities?)null).TakesFileReplacement());
    }
}
