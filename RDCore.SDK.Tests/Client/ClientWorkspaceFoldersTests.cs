using System.IO.Abstractions.TestingHelpers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using RDCore.SDK.Client;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Client;

/// <summary>
/// A client app opened on a folder of a workspace starts its server on the folder's project, and gives it every folder of the workspace, in the order its
/// <c>.rdworkspace</c> lists them (<strong>RD-VBAL §2.2.2</strong>).
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.2.2 Workspace")]
public sealed class ClientWorkspaceFoldersTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-client-workspace");

    private static WorkspaceFolderList NavalBattle(string opened)
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(Path.Combine(Root, WorkspaceFile.FileName), new MockFileData("""{ "Folders": [ "NavalLib", "NavalConsole" ] }"""));
        fileSystem.AddDirectory(Path.Combine(Root, "NavalLib"));
        fileSystem.AddDirectory(Path.Combine(Root, "NavalConsole"));
        return WorkspaceFolderList.Read(fileSystem, Path.Combine(Root, opened))!;
    }

    [TestMethod]
    public void TheServerOfAWorkspace_StartsOnThePrimaryProject_AndIsGivenEveryFolderInTheFilesOrder()
    {
        var (rootUri, folders) = RDCoreClientApp.InitializeWorkspaceOf(NavalBattle("NavalConsole"), Path.Combine(Root, "NavalConsole"));

        Assert.AreEqual(DocumentUri.From(SymbolUri.Root(Path.Combine(Root, "NavalConsole"))), rootUri);
        CollectionAssert.AreEqual(new[] { "NavalLib", "NavalConsole" }, folders!.Select(folder => folder.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "NavalLib", "NavalConsole" }.Select(name => DocumentUri.From(SymbolUri.Root(Path.Combine(Root, name)))).ToArray(),
            folders!.Select(folder => folder.Uri).ToArray());
    }

    [TestMethod]
    public void AFolderOpenedOnItsOwn_IsTheServersRoot_AndNoFoldersAreGiven()
    {
        var folder = Path.Combine(Root, "Scratch");

        var (rootUri, folders) = RDCoreClientApp.InitializeWorkspaceOf(null, folder);

        Assert.AreEqual((DocumentUri)folder, rootUri);
        Assert.IsNull(folders);
    }

    [TestMethod]
    public void AWorkspaceWithNoFolderLoaded_LeavesTheServerOnTheFolderItWasOpenedOn()
    {
        var empty = new WorkspaceFolderList { Root = Root, WorkspaceFilePath = Path.Combine(Root, WorkspaceFile.FileName) };

        var (rootUri, folders) = RDCoreClientApp.InitializeWorkspaceOf(empty, Root);

        Assert.AreEqual((DocumentUri)Root, rootUri);
        Assert.IsNull(folders);
    }

    [TestMethod]
    public void TheWorkspaceFolderSetting_IsAPath_OrTheFolderOfAFileUri()
    {
        var fileSystem = new MockFileSystem();
        var folder = Path.Combine(Root, "NavalConsole");

        Assert.AreEqual(folder, RDCoreClientApp.FolderOf(fileSystem, folder));
        Assert.AreEqual(Path.Combine("demo", "NavalConsole"), RDCoreClientApp.FolderOf(fileSystem, Path.Combine("demo", "NavalConsole")));
        Assert.AreEqual(folder, RDCoreClientApp.FolderOf(fileSystem, new Uri(folder).AbsoluteUri), ignoreCase: OperatingSystem.IsWindows());
        Assert.IsNull(RDCoreClientApp.FolderOf(fileSystem, "untitled:Untitled-1"));
        Assert.IsNull(RDCoreClientApp.FolderOf(fileSystem, ""));
    }

    // the server takes the rootUri it is sent for the workspace (WorkspaceRootChoice): a client opened on the workspace's own folder starts it on the primary
    // project too, so that it is told one folder.
    [TestMethod]
    [DataRow("", "NavalLib", DisplayName = "opened on the workspace's own folder")]
    [DataRow("NavalConsole", null, DisplayName = "opened on the primary project")]
    public void TheServerOfAWorkspace_IsStartedOnTheFolderItIsSentAsTheRoot(string opened, string? startedOn)
    {
        var workspace = NavalBattle(opened);
        var openedOn = Path.Combine(Root, opened);

        var primary = RDCoreClientApp.PrimaryFolderOf(workspace, openedOn);

        Assert.AreEqual(startedOn is null ? null : Path.Combine(Root, startedOn), primary);
        var request = new OmniSharp.Extensions.LanguageServer.Protocol.Models.InitializeParams
        {
            RootUri = RDCoreClientApp.InitializeWorkspaceOf(workspace, openedOn).RootUri,
        };
        Assert.IsFalse(RDCore.SDK.Server.WorkspaceRootChoice.Of(request, primary ?? openedOn).Differs, "the server is told one folder");
    }
}
