using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// A client opened on a folder of a workspace loads every folder its <c>.rdworkspace</c> lists, in its order, and the folder it was opened on is the
/// primary project (<strong>RD-VBAL §2.2.2</strong>).
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.2.2 Workspace")]
public sealed class WorkspaceFolderListTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-workspace-folders");
    private static readonly string WorkspaceFilePath = Path.Combine(Root, WorkspaceFile.FileName);

    private static MockFileSystem Workspace(string json, params string[] folders)
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(WorkspaceFilePath, new MockFileData(json));
        foreach (var folder in folders)
        {
            fileSystem.AddDirectory(Path.Combine(Root, folder));
        }

        return fileSystem;
    }

    private static readonly string[] NavalBattle = ["NavalLib", "NavalConsole", "NavalWindow"];
    private const string NavalBattleJson = """{ "Folders": [ "NavalLib", "NavalConsole", "NavalWindow" ] }""";

    [TestMethod]
    public void AWorkspaceOpenedOnAFolderItLists_LoadsEveryFolderInItsOrder_AndTheFolderIsThePrimary()
    {
        var list = WorkspaceFolderList.Read(Workspace(NavalBattleJson, NavalBattle), Path.Combine(Root, "NavalConsole"))!;

        CollectionAssert.AreEqual(NavalBattle, list.Folders.Select(folder => folder.Name).ToArray());
        Assert.AreEqual("NavalConsole", list.Primary!.Name);
        Assert.AreEqual(Root, list.Root);
        Assert.AreEqual(WorkspaceFilePath, list.WorkspaceFilePath);
        Assert.AreEqual(0, list.Refused.Length);
    }

    [TestMethod]
    public void AWorkspaceOpenedOnItsRoot_HasItsFirstFolderAsThePrimary()
    {
        var list = WorkspaceFolderList.Read(Workspace(NavalBattleJson, NavalBattle), Root)!;

        CollectionAssert.AreEqual(NavalBattle, list.Folders.Select(folder => folder.Name).ToArray());
        Assert.AreEqual("NavalLib", list.Primary!.Name);
    }

    [TestMethod]
    public void AFolder_IsItsPathUnderTheRoot_AndItsFileUri()
    {
        var folder = WorkspaceFolderList.Read(Workspace(NavalBattleJson, NavalBattle), Root)!.Folders[1];

        Assert.AreEqual(Path.Combine(Root, "NavalConsole"), folder.Path);
        Assert.AreEqual(SymbolUri.Root(folder.Path), folder.Uri);
        Assert.IsTrue(folder.Uri.IsFile);
    }

    [TestMethod]
    public void AFolderNoWorkspaceListsIt_IsOpenedOnItsOwn()
    {
        var fileSystem = Workspace("""{ "Folders": [ "NavalLib" ] }""", "NavalLib", "Scratch");

        Assert.IsNull(WorkspaceFolderList.Read(fileSystem, Path.Combine(Root, "Scratch")));
    }

    [TestMethod]
    public void AFolderWithNoWorkspaceAboveIt_IsOpenedOnItsOwn()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(Path.Combine(Root, "NavalLib"));

        Assert.IsNull(WorkspaceFolderList.Read(fileSystem, Path.Combine(Root, "NavalLib")));
    }

    [TestMethod]
    [DataRow("not json", DisplayName = "not JSON")]
    [DataRow("[ \"NavalLib\" ]", DisplayName = "not a JSON object")]
    public void AWorkspaceFileThatCannotBeRead_MakesNoWorkspace(string json)
        => Assert.IsNull(WorkspaceFolderList.Read(Workspace(json, "NavalLib"), Path.Combine(Root, "NavalLib")));

    // RD-VBAL §2.2.2: a workspace lists the folders directly under its own root, by their names; nothing else is loaded, and each refusal names its entry.
    [TestMethod]
    [DataRow("/srv/NavalLib", WorkspaceFolderRefusal.Absolute, DisplayName = "a rooted path")]
    [DataRow("\\\\server\\NavalLib", WorkspaceFolderRefusal.Absolute, DisplayName = "a network path")]
    [DataRow("C:\\Work\\NavalLib", WorkspaceFolderRefusal.Absolute, DisplayName = "a path with a drive")]
    [DataRow("..", WorkspaceFolderRefusal.OutsideRoot, DisplayName = "the parent folder")]
    [DataRow("../Shared", WorkspaceFolderRefusal.OutsideRoot, DisplayName = "a sibling of the root")]
    [DataRow("NavalLib\\..\\..\\Shared", WorkspaceFolderRefusal.OutsideRoot, DisplayName = "a path that climbs out")]
    [DataRow(".", WorkspaceFolderRefusal.NotDirectlyUnderRoot, DisplayName = "the root itself")]
    [DataRow("src/NavalLib", WorkspaceFolderRefusal.NotDirectlyUnderRoot, DisplayName = "a nested folder")]
    [DataRow("navallib", WorkspaceFolderRefusal.Duplicate, DisplayName = "a folder listed before, in another letter case")]
    [DataRow("NavalTests", WorkspaceFolderRefusal.Missing, DisplayName = "a folder that is not there")]
    public void AnEntryThatIsNotAFolderDirectlyUnderTheRoot_IsNotLoaded_AndIsRefused(string entry, WorkspaceFolderRefusal reason)
    {
        var json = $$"""{ "Folders": [ "NavalLib", {{System.Text.Json.JsonSerializer.Serialize(entry)}}, "NavalConsole" ] }""";
        var fileSystem = Workspace(json, "NavalLib", "NavalConsole");
        fileSystem.AddDirectory(Path.Combine(Root, "src", "NavalLib"));

        var list = WorkspaceFolderList.Read(fileSystem, Path.Combine(Root, "NavalConsole"))!;

        CollectionAssert.AreEqual(new[] { "NavalLib", "NavalConsole" }, list.Folders.Select(folder => folder.Name).ToArray());
        Assert.AreEqual(new RefusedWorkspaceFolder(reason, entry), list.Refused.Single());
        Assert.AreEqual("NavalConsole", list.Primary!.Name);
    }

    [TestMethod]
    public void AWorkspaceWhoseFoldersAreAllRefused_HasNoPrimary_AndSaysWhy()
    {
        var list = WorkspaceFolderList.Read(Workspace("""{ "Folders": [ "..", "NavalTests" ] }"""), Root)!;

        Assert.AreEqual(0, list.Folders.Length);
        Assert.IsNull(list.Primary);
        CollectionAssert.AreEqual(
            new[] { WorkspaceFolderRefusal.OutsideRoot, WorkspaceFolderRefusal.Missing }, list.Refused.Select(refused => refused.Reason).ToArray());
    }

    [TestMethod]
    [DataRow("en-US", "climbs out of the workspace root", DisplayName = "in English")]
    [DataRow("fr-CA", "sort de la racine de l'espace de travail", DisplayName = "in French")]
    public void ARefusal_NamesItsEntryAndTheFile_InTheReadersLanguage(string culture, string expected)
    {
        var message = new RefusedWorkspaceFolder(WorkspaceFolderRefusal.OutsideRoot, "../Shared").ToMessage(WorkspaceFilePath, new CultureInfo(culture));

        StringAssert.Contains(message, "../Shared");
        StringAssert.Contains(message, WorkspaceFilePath);
        StringAssert.Contains(message, expected);
    }

    [TestMethod]
    public void EveryRefusal_HasAMessageInEachLanguage()
    {
        foreach (var reason in Enum.GetValues<WorkspaceFolderRefusal>())
        {
            foreach (var culture in new[] { "en-US", "fr-CA" })
            {
                var message = new RefusedWorkspaceFolder(reason, "X").ToMessage("W", new CultureInfo(culture));
                Assert.AreNotEqual("X: W", message, $"{reason} ({culture})");
            }
        }
    }
}
