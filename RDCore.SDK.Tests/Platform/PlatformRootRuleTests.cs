using System.IO.Abstractions;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// Where a process's platform root comes from. <c>rdc</c> sits at the root, so its root is its own folder, which holds the manifest, or the
/// root its command line names; the variable it inherited is never read, unless it runs as a server of the process that started it, which gives
/// it its root. A built program's root is its output folder.
/// </summary>
/// <remarks>
/// These set the process's <c>RDCORE_PLATFORM_ROOT</c>, and one writes a manifest into the test's own folder (<see cref="AppContext.BaseDirectory"/>),
/// the folder of the process at the root here: they run on their own, and put both back.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class PlatformRootRuleTests
{
    private string _folder = null!;
    private string _elsewhere = null!;
    private string? _inherited;
    private string? _mode;

    private static string OwnFolder => Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    private static string OwnManifest => Path.Combine(OwnFolder, PlatformManifest.FileName);

    [TestInitialize]
    public void NameAnotherTree()
    {
        _inherited = Environment.GetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable);
        _mode = Environment.GetEnvironmentVariable(RDCore.SDK.Client.RDCoreServerProcess.ModeEnvironmentVariable);
        Environment.SetEnvironmentVariable(RDCore.SDK.Client.RDCoreServerProcess.ModeEnvironmentVariable, null);
        _folder = Path.Combine(Path.GetTempPath(), "RDCore", "root-rule", Guid.NewGuid().ToString("N"));
        _elsewhere = Path.Combine(_folder, "another-tree");
        Directory.CreateDirectory(_elsewhere);
        File.WriteAllText(Path.Combine(_elsewhere, PlatformManifest.FileName), "{}");
        Assert.IsFalse(File.Exists(OwnManifest), $"the test's own folder holds no manifest of its own: {OwnManifest}");

        // what a shell set for another tree: a process at the root does not read it.
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, _elsewhere);
    }

    [TestCleanup]
    public void PutBack()
    {
        File.Delete(OwnManifest);
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, _inherited);
        Environment.SetEnvironmentVariable(RDCore.SDK.Client.RDCoreServerProcess.ModeEnvironmentVariable, _mode);
        Directory.Delete(_folder, recursive: true);
    }

    [TestMethod]
    public void AProcessAtTheRoot_IsRootedInItsOwnFolder_OverTheVariableItInherited()
    {
        File.WriteAllText(OwnManifest, "{}");

        var root = PlatformEnvironment.ApplyOwnFolderAsRoot([]);

        Assert.AreEqual(OwnFolder, root);
        Assert.AreEqual(OwnFolder, Environment.GetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable), "the processes it starts are told");
        Assert.AreEqual(OwnFolder, new PlatformEnvironment(new FileSystem()).Root);
    }

    // the tree is named, never searched for: the folder above the test's is not tried, nor the variable.
    [TestMethod]
    public void AProcessAtTheRoot_InAFolderWithNoManifest_IsTheErrorThatNamesTheLayoutAndTheArgument()
    {
        var error = Assert.ThrowsExactly<NotAPlatformRootException>(() => PlatformEnvironment.ApplyOwnFolderAsRoot([]));

        Assert.AreEqual(OwnFolder, error.Folder);
        StringAssert.Contains(error.Message, OwnFolder);
        StringAssert.Contains(error.Message, PlatformManifest.FileName);
        StringAssert.Contains(error.Message, "--platform-root");
        Assert.AreEqual(_elsewhere, Environment.GetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable), "nothing is applied");
    }

    // a test's root, or an IDE launcher's: what the command line names is the root, whether or not the process's own folder is one.
    [TestMethod]
    [DataRow(false, DisplayName = "its own folder holds no manifest")]
    [DataRow(true, DisplayName = "its own folder is a root too")]
    public void AProcessAtTheRoot_IsRootedWhereItsCommandLineSays(bool ownFolderIsARoot)
    {
        if (ownFolderIsARoot)
        {
            File.WriteAllText(OwnManifest, "{}");
        }

        var named = Path.Combine(_folder, "named");

        var root = PlatformEnvironment.ApplyOwnFolderAsRoot(["--trace-level", "Debug", "--platform-root", named]);

        Assert.AreEqual(named, root);
        Assert.AreEqual(named, new PlatformEnvironment(new FileSystem()).Root);
    }

    // a built program's output folder holds what it runs with, and no manifest: it is the program's root as it is.
    [TestMethod]
    public void AFolderMadeTheRoot_IsTheRootOfTheProcess_WhetherOrNotItHoldsAManifest()
    {
        var output = Path.Combine(_folder, "Ledger", "bin") + Path.DirectorySeparatorChar;

        var root = PlatformEnvironment.ApplyRoot(output);

        Assert.AreEqual(Path.Combine(_folder, "Ledger", "bin"), root);
        Assert.AreEqual(root, Environment.GetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable));
        Assert.AreEqual(root, new PlatformEnvironment(new FileSystem()).Root);
    }

    // an environment host or an external host that a process started is given that process's root, which it keeps wherever its executable is: the test
    // run's folder holds an rdc, and no manifest.
    [TestMethod]
    [DataRow("host", DisplayName = "an environment host")]
    [DataRow("external", DisplayName = "an external host")]
    public void AServerOfAnotherProcess_IsRootedWhereThatProcessSays_InAFolderWithNoManifest(string mode)
    {
        Environment.SetEnvironmentVariable(RDCore.SDK.Client.RDCoreServerProcess.ModeEnvironmentVariable, mode);

        var root = PlatformEnvironment.ApplyOwnFolderAsRoot([]);

        Assert.AreEqual(_elsewhere, root);
        Assert.AreEqual(_elsewhere, new PlatformEnvironment(new FileSystem()).Root);
    }

    // a server that was given no root is a process at the root as any other: its own folder, which must hold the manifest.
    [TestMethod]
    public void AServerOfAnotherProcess_GivenNoRoot_IsRootedInItsOwnFolder()
    {
        Environment.SetEnvironmentVariable(RDCore.SDK.Client.RDCoreServerProcess.ModeEnvironmentVariable, "host");
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, null);

        var error = Assert.ThrowsExactly<NotAPlatformRootException>(() => PlatformEnvironment.ApplyOwnFolderAsRoot([]));
        File.WriteAllText(OwnManifest, "{}");

        Assert.AreEqual(OwnFolder, error.Folder);
        Assert.AreEqual(OwnFolder, PlatformEnvironment.ApplyOwnFolderAsRoot([]));
    }

    // what a verb parses once the root is applied: the argument, in either spelling, is not among its arguments, and the others are, in their order.
    [TestMethod]
    [DataRow(new[] { "bench", "--runs", "3", "--platform-root", "C:/tree" }, new[] { "bench", "--runs", "3" }, DisplayName = "the path after it")]
    [DataRow(new[] { "--platform-root=C:/tree", "describe-ext", "Ext" }, new[] { "describe-ext", "Ext" }, DisplayName = "the path in it")]
    [DataRow(new[] { "run", "Ledger" }, new[] { "run", "Ledger" }, DisplayName = "no root named")]
    public void TheArgumentsOfAVerb_AreTheCommandLine_WithoutTheRootItNames(string[] args, string[] verb)
        => CollectionAssert.AreEqual(verb, RDCore.SDK.Server.Configuration.SdkCommandLine.WithoutPlatformRoot(args));
}
