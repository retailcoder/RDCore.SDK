using RDCore.SDK.Platform;
using RDCore.SDK.Server.Configuration;
using System.IO.Abstractions;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// The platform root a command line names (<c>--platform-root</c>) is the process's, over what <c>RDCORE_PLATFORM_ROOT</c> said when it started
/// (<see cref="SdkCommandLine.ApplyPlatformRoot(string[])"/>).
/// </summary>
// mutates the RDCORE_PLATFORM_ROOT process environment variable.
[TestClass]
[DoNotParallelize]
public sealed class PlatformRootArgumentTests
{
    private static readonly string Started = Path.Combine(Path.GetTempPath(), "RDCore", "root-the-process-started-with");
    private static readonly string Named = Path.Combine(Path.GetTempPath(), "RDCore", "root-the-command-line-names");

    private string? _inherited;

    [TestInitialize]
    public void StartWithARoot()
    {
        _inherited = Environment.GetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable);
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, Started);
    }

    [TestCleanup]
    public void RestoreTheRoot() => Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, _inherited);

    [TestMethod]
    [DataRow(false, DisplayName = "--platform-root <path>")]
    [DataRow(true, DisplayName = "--platform-root=<path>")]
    public void TheRootTheCommandLineNames_IsThePlatformRoot_OverTheEnvironmentVariable(bool joined)
    {
        string[] args = joined ? ["--workspace", "C:/ws", $"--platform-root={Named}"] : ["--workspace", "C:/ws", "--platform-root", Named];

        var applied = SdkCommandLine.ApplyPlatformRoot(args);

        Assert.AreEqual(Named, applied);
        Assert.AreEqual(Named, new PlatformEnvironment(new FileSystem()).Root);
    }

    [TestMethod]
    public void ACommandLineThatNamesNoRoot_LeavesTheEnvironmentVariablesRoot()
    {
        Assert.IsNull(SdkCommandLine.ApplyPlatformRoot(["--workspace", "C:/ws"]));
        Assert.AreEqual(Started, new PlatformEnvironment(new FileSystem()).Root);
    }

    [TestMethod]
    public void ACommandLineThatNamesTheRoot_IsNotAnswered_ButRunWith()
        => Assert.IsFalse(SdkCommandLine.TryAnswer(["--workspace", "C:/ws", "--platform-root", Named], out _));
}
