using System.IO.Abstractions.TestingHelpers;
using System.Text;
using System.Text.Json;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// A <c>.rdworkspace</c> lists the folders of a workspace's projects and the files of its root, and is read leniently, as a file written by hand needs
/// (<strong>RD-VBAL §2.2.2</strong>).
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.2.2 Workspace")]
public sealed class WorkspaceFileTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-workspace-file");
    private static readonly string WorkspaceFilePath = Path.Combine(Root, WorkspaceFile.FileName);

    private static async Task<WorkspaceFile> ReadAsync(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await WorkspaceFile.ReadAsync(stream);
    }

    [TestMethod]
    public async Task AWorkspaceFile_IsReadInItsOrder()
    {
        var workspace = await ReadAsync("""{ "Folders": [ "NavalLib", "NavalConsole" ], "Files": [ "README.md" ] }""");

        CollectionAssert.AreEqual(new[] { "NavalLib", "NavalConsole" }, workspace.Folders);
        CollectionAssert.AreEqual(new[] { "README.md" }, workspace.Files);
        Assert.IsNull(workspace.ExtensionData);
    }

    [TestMethod]
    [DataRow("""{ }""", DisplayName = "no lists")]
    [DataRow("""{ "Folders": null, "Files": null }""", DisplayName = "null lists")]
    [DataRow("""{ "Folders": "NavalLib", "Files": { "README.md": true } }""", DisplayName = "lists that are not lists")]
    [DataRow("""{ "Folders": [ null, 1, "", "  ", [ "NavalLib" ], { } ], "Files": [ false ] }""", DisplayName = "items that name nothing")]
    public async Task WhatNamesNothing_IsLeftOut(string json)
    {
        var workspace = await ReadAsync(json);

        Assert.IsEmpty(workspace.Folders);
        Assert.IsEmpty(workspace.Files);
    }

    [TestMethod]
    public async Task TheItemsThatNameSomething_AreKept_AmongOnesThatDoNot()
    {
        var workspace = await ReadAsync("""{ "Folders": [ "NavalLib", 2, { "Name": "x" }, "NavalConsole", "" ] }""");

        CollectionAssert.AreEqual(new[] { "NavalLib", "NavalConsole" }, workspace.Folders);
    }

    [TestMethod]
    [DataRow("null", DisplayName = "null")]
    [DataRow("[ \"NavalLib\" ]", DisplayName = "an array")]
    [DataRow("{ \"Folders\": [ ", DisplayName = "not JSON")]
    public async Task AFileThatIsNotAJsonObject_IsRefused(string json)
    {
        await Assert.ThrowsAsync<JsonException>(() => ReadAsync(json));
    }

    [TestMethod]
    public async Task TheMembersTheModelDoesNotKnow_AreWrittenBack()
    {
        var workspace = await ReadAsync("""{ "Folders": [ "NavalLib" ], "Startup": "NavalConsole" }""");

        var written = JsonSerializer.Serialize(workspace.WithFolder("NavalConsole"));
        var read = await ReadAsync(written);

        CollectionAssert.AreEqual(new[] { "NavalLib", "NavalConsole" }, read.Folders);
        Assert.AreEqual("NavalConsole", read.ExtensionData!["Startup"].GetString());
    }

    [TestMethod]
    public void WithFolder_AddsItLast_UnlessItIsListedInAnyCase()
    {
        var workspace = new WorkspaceFile { Folders = ["NavalLib"] };

        CollectionAssert.AreEqual(new[] { "NavalLib", "NavalConsole" }, workspace.WithFolder("NavalConsole").Folders);
        Assert.AreSame(workspace, workspace.WithFolder("NAVALLIB"));
    }

    [TestMethod]
    public void WithoutFolder_RemovesItInAnyCase()
    {
        var workspace = new WorkspaceFile { Folders = ["NavalLib", "NavalConsole"], Files = ["README.md"] };

        var without = workspace.WithoutFolder("navallib");

        CollectionAssert.AreEqual(new[] { "NavalConsole" }, without.Folders);
        CollectionAssert.AreEqual(new[] { "README.md" }, without.Files);
    }

    [TestMethod]
    public async Task TheLoader_ReadsNothing_FromAFolderWithoutAWorkspaceFile()
    {
        var sut = new WorkspaceFileLoader(new MockFileSystem());

        Assert.IsFalse(sut.TryLocate(Root, out var path));
        Assert.AreEqual(WorkspaceFilePath, path);
        Assert.IsNull(await sut.LoadAsync(Root));
    }

    [TestMethod]
    [DataRow("{ \"Folders\": [ ", DisplayName = "not JSON")]
    [DataRow("[ \"NavalLib\" ]", DisplayName = "not an object")]
    public async Task TheLoader_ReadsNothing_FromAWorkspaceFileItCannotRead(string json)
    {
        var sut = new WorkspaceFileLoader(new MockFileSystem(new Dictionary<string, MockFileData> { [WorkspaceFilePath] = new(json) }));

        Assert.IsTrue(sut.TryLocate(Root, out _));
        Assert.IsNull(await sut.LoadAsync(Root));
    }

    [TestMethod]
    public async Task TheLoader_WritesTheWorkspaceFileAtTheRoot_AndReadsItBack()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(Root);
        var sut = new WorkspaceFileLoader(fileSystem);

        await sut.SaveAsync(Root, new WorkspaceFile { Folders = ["NavalLib", "NavalConsole"], Files = ["README.md"] });
        var loaded = await sut.LoadAsync(Root);

        Assert.IsTrue(fileSystem.File.Exists(WorkspaceFilePath));
        Assert.IsNotNull(loaded);
        CollectionAssert.AreEqual(new[] { "NavalLib", "NavalConsole" }, loaded.Folders);
        CollectionAssert.AreEqual(new[] { "README.md" }, loaded.Files);
    }
}
