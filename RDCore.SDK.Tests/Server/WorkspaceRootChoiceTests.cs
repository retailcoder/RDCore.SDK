using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Client;
using RDCore.SDK.Server;
using RDCore.SDK.Server.Configuration;

namespace RDCore.Tests.Server;

/// <summary>
/// A server is told its workspace twice: by the folder its client opened, in the <c>initialize</c> request, and by the folder it was started on
/// (<c>--workspace</c>). The client's folder wins, and the one it was started on is the workspace only when the client opened none. Two ways of writing one
/// folder are one folder; when the two are different folders, the server's settings and its children are the client's folder's, and the user is told.
/// </summary>
[TestClass]
public sealed class WorkspaceRootChoiceTests
{
    // a space in a folder's name is escaped in its URI.
    private static readonly string Parent = Path.Combine(Path.GetTempPath(), "rdcore root split");
    private static readonly string Project = Path.Combine(Parent, "Project1");

    private static InitializeParams Request(string? rootUri = null, params string[] workspaceFolders) => new()
    {
        RootUri = rootUri is null ? null : DocumentUri.From(rootUri),
        WorkspaceFolders = workspaceFolders.Length == 0 ? null
            : new Container<WorkspaceFolder>(workspaceFolders.Select(folder => new WorkspaceFolder { Uri = DocumentUri.From(folder), Name = "folder" })),
    };

    private static string UriOf(string folder) => new Uri(folder).AbsoluteUri;

    // ---- which folder it is ----

    [TestMethod]
    [DataRow("separator", DisplayName = "started on it with a separator at its end")]
    [DataRow("uri", DisplayName = "started on its file URI, a space escaped")]
    public void OneFolderWrittenTwoWays_IsOneFolder(string startedOn)
    {
        var started = startedOn == "separator" ? Project + Path.DirectorySeparatorChar : UriOf(Project);
        Assert.AreNotEqual(Project, started, "the folder is to be written another way than the request writes it");

        var root = WorkspaceRootChoice.Of(Request(UriOf(Project)), started);

        Assert.IsFalse(root.Differs);
        Assert.IsNull(root.Message);
        Assert.IsTrue(RDCore.SDK.Workspace.WorkspacePaths.Same(Project, root.Root!), root.Root);
    }

    [TestMethod]
    public void OneFolder_WhoseDriveIsWrittenInAnotherCase_IsOneFolder_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("a drive is Windows' own");
        }

        // the drive of the folder as the request gives it, in the other case.
        var requested = WorkspaceRootChoice.Of(Request(UriOf(Project)), null).Requested!;
        var drive = char.IsUpper(requested[0]) ? char.ToLowerInvariant(requested[0]) : char.ToUpperInvariant(requested[0]);
        Assert.IsTrue(char.IsLetter(drive) && requested[1] == ':', requested);

        Assert.IsFalse(WorkspaceRootChoice.Of(Request(UriOf(Project)), drive + requested[1..]).Differs);
    }

    [TestMethod]
    public void TwoFolders_AreTwo_AndTheOneTheClientOpenedIsTheRoot()
    {
        var other = Path.Combine(Parent, "Project2");

        var root = WorkspaceRootChoice.Of(Request(UriOf(Project)), other);

        Assert.IsTrue(root.Differs);
        Assert.IsTrue(RDCore.SDK.Workspace.WorkspacePaths.Same(Project, root.Root!), root.Root);
        StringAssert.Contains(root.Message, root.Requested, "the folder the client opened");
        StringAssert.Contains(root.Message, other, "the folder the server was started on");
    }

    [TestMethod]
    public void ARequestWithNoRootUri_GivesItsFirstWorkspaceFolder()
    {
        var root = WorkspaceRootChoice.Of(Request(null, UriOf(Parent), UriOf(Project)), Project);

        Assert.IsTrue(root.Differs, "the server was started on its second folder");
        Assert.IsTrue(RDCore.SDK.Workspace.WorkspacePaths.Same(Parent, root.Root!), root.Root);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no root and no folders")]
    [DataRow("untitled:Untitled-1", DisplayName = "a root that is not a folder")]
    public void ARequestThatOpensNoFolder_LeavesTheOneTheServerWasStartedOn(string? rootUri)
    {
        var root = WorkspaceRootChoice.Of(Request(rootUri), Project);

        Assert.IsNull(root.Requested);
        Assert.IsFalse(root.Differs);
        Assert.AreEqual(Project, root.Root);
    }

    // ---- what the server does with it ----

    [TestMethod]
    public void WhenTheTwoDiffer_TheServersChildrenAreStartedOnTheFolderTheClientOpened()
    {
        var workspace = new SdkWorkspaceOptions { WorkspaceUri = Project };

        var said = RDCoreServerApp.TakeWorkspaceRoot(WorkspaceRootChoice.Of(Request(UriOf(Parent)), workspace.WorkspaceUri), workspace, settings: null);

        Assert.IsNotNull(said, "the user is told");
        // a quoted run is one argument: the folder has spaces in its name.
        var arguments = System.Text.RegularExpressions.Regex.Matches(
            RDCoreServerProcess.ServerArguments(1, "pipe", workspace.WorkspaceUri, LogLevel.Trace, verbose: false), "\"[^\"]*\"|\\S+")
            .Select(match => match.Value).ToArray();
        var startedOn = arguments[Array.IndexOf(arguments, "-w") + 1].Trim('"');
        Assert.IsTrue(RDCore.SDK.Workspace.WorkspacePaths.Same(Parent, startedOn), startedOn);
    }

    [TestMethod]
    public void WhenTheTwoAreOne_NothingIsTold_AndTheWorkspaceIsAsTheServerWasStarted()
    {
        var started = Project + Path.DirectorySeparatorChar;
        var workspace = new SdkWorkspaceOptions { WorkspaceUri = started };

        var said = RDCoreServerApp.TakeWorkspaceRoot(WorkspaceRootChoice.Of(Request(UriOf(Project)), started), workspace, settings: null);

        Assert.IsNull(said);
        Assert.AreEqual(started, workspace.WorkspaceUri);
    }
}
