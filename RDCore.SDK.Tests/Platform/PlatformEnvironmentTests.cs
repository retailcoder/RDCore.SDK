using System.IO.Abstractions.TestingHelpers;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

// mutates the RDCORE_PLATFORM_ROOT process environment variable.
[TestClass]
[DoNotParallelize]
public class PlatformEnvironmentTests
{
    private readonly MockFileSystem _fileSystem = new();
    private string _root = null!;

    [TestInitialize]
    public void SetRootOverride()
    {
        _root = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), "rdcore-platform"));
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, _root);
    }

    [TestCleanup]
    public void ClearRootOverride()
        => Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, TestPlatformRoot.Path);

    private IPlatformEnvironment CreateSut() => new PlatformEnvironment(_fileSystem);

    [TestMethod]
    public void Root_UsesTheEnvironmentOverrideWhenSet()
        => Assert.AreEqual(_root, CreateSut().Root);

    [TestMethod]
    public void ManifestPath_IsRdcoreJsonUnderTheRoot()
        => Assert.AreEqual(_fileSystem.Path.Combine(_root, "rdcore.json"), CreateSut().ManifestPath);

    [TestMethod]
    public void Resolve_NormalisesForwardSlashesAndJoinsUnderTheRoot()
        => Assert.AreEqual(
            _fileSystem.Path.Combine(_root, "RDCore.Parsing", "RDCore.ParseServer.exe"),
            CreateSut().Resolve("RDCore.Parsing/RDCore.ParseServer.exe"));

    // a platform is installed where its user may not write (Program Files, /opt): what it writes goes to the user's folders.
    [TestMethod]
    public void LogsDirectory_IsTheUsersAndCreated_NotUnderTheRoot()
    {
        using var _ = Override(PlatformEnvironment.LogsEnvironmentVariable, null);

        var logs = CreateSut().LogsDirectory;

        Assert.AreEqual(UsersLogsDirectory(), logs);
        Assert.IsFalse(logs.StartsWith(_root, StringComparison.OrdinalIgnoreCase), logs);
        Assert.IsTrue(_fileSystem.Directory.Exists(logs));
    }

    [TestMethod]
    public void LogsDirectory_IsTheOverrideWhenSet()
    {
        var elsewhere = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), "rdcore-logs-elsewhere"));
        using var _ = Override(PlatformEnvironment.LogsEnvironmentVariable, elsewhere);

        var logs = CreateSut().LogsDirectory;

        Assert.AreEqual(elsewhere, logs);
        Assert.IsTrue(_fileSystem.Directory.Exists(logs));
    }

    // the audit log is the deployment's record: under the platform root, where a user's logs override does not move it, and made when it is first written.
    [TestMethod]
    public void AuditsDirectory_IsUnderTheRoot_WhereverTheLogsAre_AndNotMadeWhenAskedFor()
    {
        var elsewhere = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), "rdcore-logs-elsewhere"));
        using var _ = Override(PlatformEnvironment.LogsEnvironmentVariable, elsewhere);

        var audits = CreateSut().AuditsDirectory;

        Assert.AreEqual(_fileSystem.Path.Combine(_root, "Logs", "Audits"), audits);
        Assert.IsFalse(_fileSystem.Directory.Exists(audits));
    }

    // a process is given its root, and none is guessed: with none given, each member under the root is the error that says how to give one.
    [TestMethod]
    [DataRow(nameof(IPlatformEnvironment.Root), null)]
    [DataRow(nameof(IPlatformEnvironment.Root), "   ")]
    [DataRow(nameof(IPlatformEnvironment.ManifestPath), null)]
    [DataRow(nameof(IPlatformEnvironment.SettingsPath), null)]
    [DataRow(nameof(IPlatformEnvironment.AuditsDirectory), null)]
    [DataRow(nameof(IPlatformEnvironment.Resolve), null)]
    public void AMemberUnderTheRoot_WithNoRootGiven_IsTheErrorThatNamesBothWaysToGiveOne(string member, string? given)
    {
        using var _ = Override(PlatformEnvironment.RootEnvironmentVariable, given);
        var sut = CreateSut();

        var error = Assert.ThrowsExactly<PlatformRootNotGivenException>(() => Read(sut, member));

        StringAssert.Contains(error.Message, "--platform-root");
        StringAssert.Contains(error.Message, PlatformEnvironment.RootEnvironmentVariable);
    }

    // what is the user's and not the platform's is read with no root given: a process that needs no platform path needs no root.
    [TestMethod]
    public void TheLogsAndTheUsersFolders_WithNoRootGiven_AreRead()
    {
        var logs = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), "rdcore-logs-elsewhere"));
        var user = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), "rdcore-user-elsewhere"));
        using var root = Override(PlatformEnvironment.RootEnvironmentVariable, null);
        using var logsOverride = Override(PlatformEnvironment.LogsEnvironmentVariable, logs);
        using var userOverride = Override(PlatformEnvironment.UserDataEnvironmentVariable, user);
        var sut = CreateSut();

        Assert.AreEqual(logs, sut.LogsDirectory);
        Assert.AreEqual(user, sut.UserDataDirectory);
    }

    // the root is read when a member first needs it, not when the environment is made, and a read that found none keeps nothing.
    [TestMethod]
    public void TheRootGivenAfterTheEnvironmentIsMade_AndAfterAReadThatFoundNone_IsTheRoot()
    {
        IPlatformEnvironment sut;
        using (Override(PlatformEnvironment.RootEnvironmentVariable, null))
        {
            sut = CreateSut();
            Assert.ThrowsExactly<PlatformRootNotGivenException>(() => sut.Root);
        }

        Assert.AreEqual(_root, sut.Root);
    }

    private static string Read(IPlatformEnvironment environment, string member) => member switch
    {
        nameof(IPlatformEnvironment.Root) => environment.Root,
        nameof(IPlatformEnvironment.ManifestPath) => environment.ManifestPath,
        nameof(IPlatformEnvironment.SettingsPath) => environment.SettingsPath,
        nameof(IPlatformEnvironment.AuditsDirectory) => environment.AuditsDirectory,
        nameof(IPlatformEnvironment.Resolve) => environment.Resolve("RDCore.Parsing/RDCore.ParseServer.exe"),
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, null),
    };

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [DataRow("/var/xdg-state", "/var/xdg-state/rdcore/logs", DisplayName = "XDG_STATE_HOME set")]
    [DataRow("relative/state", null, DisplayName = "a relative XDG_STATE_HOME is ignored")]
    [DataRow(null, null, DisplayName = "XDG_STATE_HOME not set: ~/.local/state")]
    public void LogsDirectory_OnLinux_IsInTheUsersXdgState(string? xdgStateHome, string? expected)
    {
        using var logs = Override(PlatformEnvironment.LogsEnvironmentVariable, null);
        using var state = Override("XDG_STATE_HOME", xdgStateHome);

        Assert.AreEqual(
            expected ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state", "rdcore", "logs"),
            CreateSut().LogsDirectory);
    }

    private static string UsersLogsDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDCore", "Logs");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Logs", "RDCore");
        }

        return Environment.GetEnvironmentVariable("XDG_STATE_HOME") is { Length: > 0 } xdg && Path.IsPathRooted(xdg)
            ? Path.Combine(xdg, "rdcore", "logs")
            : Path.Combine(home, ".local", "state", "rdcore", "logs");
    }

    // sets a process environment variable for the length of a test, and puts it back as it was.
    private static Restore Override(string variable, string? value)
    {
        var was = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, value);
        return new Restore(variable, was);
    }

    private sealed record Restore(string Variable, string? Was) : IDisposable
    {
        public void Dispose() => Environment.SetEnvironmentVariable(Variable, Was);
    }
}
