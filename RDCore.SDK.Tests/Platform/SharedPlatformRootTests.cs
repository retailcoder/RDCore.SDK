using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// The shared platform root every program of a user deploys the platform into: its layout, <c>installer.json</c>, <c>install.lock</c>,
/// and the removal of old versions.
/// </summary>
[TestClass]
public sealed class SharedPlatformRootTests
{
    private static readonly string LanguageServer = OperatingSystem.IsWindows() ? "RDCore.LanguageServer.exe" : "RDCore.LanguageServer";

    private string _folder = null!;
    private SharedPlatformRoot _root = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _folder = Path.Combine(Path.GetTempPath(), "RDCore", "shared", Guid.NewGuid().ToString("N"));
        _root = new SharedPlatformRoot(_folder);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static PlatformVersion V(string text) => PlatformVersion.Parse(text);

    // a whole platform at the folder: its manifest, and the language server it names.
    private static void WritePlatform(string folder, string version)
    {
        Directory.CreateDirectory(Path.Combine(folder, "RDCore.LanguageServer"));
        File.WriteAllText(Path.Combine(folder, PlatformManifest.FileName),
            $$"""{ "platformVersion": "{{version}}", "langService": "RDCore.LanguageServer/{{LanguageServer}}" }""");
        File.WriteAllText(Path.Combine(folder, "RDCore.LanguageServer", LanguageServer), string.Empty);
    }

    private void Deploy(string version)
    {
        WritePlatform(_root.PartialRoot(V(version)), version);
        _root.MoveIntoPlace(V(version));
    }

    [TestMethod]
    public void AVersion_IsDeployedUnderVersions_NamedByItsText_ThroughAPartialFolder()
    {
        Assert.AreEqual(Path.Combine(_folder, "versions", "0.2.0-rc.1"), _root.VersionRoot(V("V0.2.0-RC.1")));
        Assert.AreEqual(Path.Combine(_folder, "versions", "0.2.0-rc.1.partial"), _root.PartialRoot(V("0.2.0-rc.1")));

        Deploy("0.2.0-rc.1");

        Assert.IsTrue(SharedPlatformRoot.IsWhole(_root.VersionRoot(V("0.2.0-rc.1"))));
        Assert.IsFalse(Directory.Exists(_root.PartialRoot(V("0.2.0-rc.1"))));
    }

    [TestMethod]
    public void APartialFolderThatIsNotAWholePlatform_IsNotMovedIntoPlace()
    {
        Directory.CreateDirectory(_root.PartialRoot(V("0.1.0")));

        Assert.ThrowsExactly<FileNotFoundException>(() => _root.MoveIntoPlace(V("0.1.0")));
        Assert.IsFalse(Directory.Exists(_root.VersionRoot(V("0.1.0"))));
    }

    [TestMethod]
    public void ABrokenFolderOfTheVersion_IsReplaced()
    {
        Directory.CreateDirectory(_root.VersionRoot(V("0.1.0")));
        File.WriteAllText(Path.Combine(_root.VersionRoot(V("0.1.0")), "left-over.txt"), string.Empty);

        Deploy("0.1.0");

        Assert.IsTrue(SharedPlatformRoot.IsWhole(_root.VersionRoot(V("0.1.0"))));
        Assert.IsFalse(File.Exists(Path.Combine(_root.VersionRoot(V("0.1.0")), "left-over.txt")));
    }

    [TestMethod]
    public void TheCurrentVersion_IsTheOneInstallerJsonNames_WithTheOneBeforeIt()
    {
        Assert.IsNull(_root.Current);

        Deploy("0.1.0");
        Assert.IsTrue(_root.MakeCurrentUnlessOlder(V("0.1.0")));
        Deploy("0.2.0");
        Assert.IsTrue(_root.MakeCurrentUnlessOlder(V("0.2.0")));

        Assert.AreEqual(new DeployedPlatform(V("0.2.0"), _root.VersionRoot(V("0.2.0"))), _root.Current);
        Assert.AreEqual(V("0.1.0"), _root.Previous);
        Assert.AreEqual("""{"Current":"0.2.0","Previous":"0.1.0"}""", File.ReadAllText(Path.Combine(_folder, SharedPlatformRoot.StateFileName)));
    }

    [TestMethod]
    public void AnOlderVersion_IsDeployedBeside_AndNotMadeCurrent()
    {
        Deploy("0.2.0");
        _root.MakeCurrentUnlessOlder(V("0.2.0"));
        Deploy("0.1.0");

        Assert.IsFalse(_root.MakeCurrentUnlessOlder(V("0.1.0")));
        Assert.AreEqual(V("0.2.0"), _root.Current?.Version);
        Assert.IsTrue(SharedPlatformRoot.IsWhole(_root.VersionRoot(V("0.1.0"))));
    }

    [TestMethod]
    public void MakingTheCurrentVersionCurrentAgain_KeepsTheOneBeforeIt()
    {
        Deploy("0.1.0");
        _root.MakeCurrentUnlessOlder(V("0.1.0"));
        Deploy("0.2.0");
        _root.MakeCurrentUnlessOlder(V("0.2.0"));

        Assert.IsTrue(_root.MakeCurrentUnlessOlder(V("0.2.0")));
        Assert.AreEqual(V("0.1.0"), _root.Previous);
    }

    [TestMethod]
    [DataRow("""{"Current":"0.3.0"}""", DisplayName = "a version that isn't deployed")]
    [DataRow("""{"Current":"not a version"}""", DisplayName = "not a version")]
    [DataRow("""{ not json""", DisplayName = "not JSON")]
    public void AStateFileThatNamesNoDeployedPlatform_NamesNoCurrentOne(string state)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, SharedPlatformRoot.StateFileName), state);

        Assert.IsNull(_root.Current);
    }

    [TestMethod]
    public void ACurrentVersionWhoseLanguageServerIsGone_IsNoCurrentOne()
    {
        Deploy("0.1.0");
        _root.MakeCurrentUnlessOlder(V("0.1.0"));
        File.Delete(Path.Combine(_root.VersionRoot(V("0.1.0")), "RDCore.LanguageServer", LanguageServer));

        Assert.IsNull(_root.Current);
    }

    [TestMethod]
    public void OldVersions_AreRemoved_ButTheCurrentOne_TheOneBeforeIt_TheOneKept_AndFoldersThatAreNoVersion()
    {
        foreach (var version in new[] { "0.1.0", "0.2.0", "0.3.0", "0.4.0" })
        {
            Deploy(version);
        }

        _root.MakeCurrentUnlessOlder(V("0.2.0"));
        _root.MakeCurrentUnlessOlder(V("0.3.0"));
        Directory.CreateDirectory(Path.Combine(_root.Versions, "notes"));
        Directory.CreateDirectory(Path.Combine(_root.Versions, "0.0.9" + SharedPlatformRoot.RemovingSuffix));

        _root.RemoveOldVersions(keep: V("0.4.0"));

        CollectionAssert.AreEquivalent(
            new[] { "0.2.0", "0.3.0", "0.4.0", "notes" },
            Directory.EnumerateDirectories(_root.Versions).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void RemovingOldVersions_FromARootWithNone_DoesNothing() => _root.RemoveOldVersions(keep: V("0.1.0"));

    [TestMethod]
    public async Task TheRoot_IsHeldByOneDeploymentAtATime_AndTheWaitingOneIsToldOnce()
    {
        var told = 0;
        var first = await _root.LockAsync(null, TestContext.CancellationToken);
        var second = _root.LockAsync(() => told++, TestContext.CancellationToken);

        await Task.Delay(600, TestContext.CancellationToken);
        Assert.IsFalse(second.IsCompleted, "the second waits while the first holds the root");

        first.Dispose();
        (await second.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken)).Dispose();
        Assert.AreEqual(1, told);
        Assert.IsTrue(File.Exists(Path.Combine(_folder, SharedPlatformRoot.LockFileName)));
    }

    [TestMethod]
    public async Task AWaitForTheRoot_StopsWhenItIsCancelled()
    {
        using var first = await _root.LockAsync(null, TestContext.CancellationToken);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _root.LockAsync(null, cancel.Token));
    }

    [TestMethod]
    public void ThisUsersRoot_IsRDCorePlatform_InTheLocalApplicationData()
        => Assert.AreEqual(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDCore", "Platform"),
            SharedPlatformRoot.ForThisUser);
}
