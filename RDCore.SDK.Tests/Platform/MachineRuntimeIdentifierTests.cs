using System.Runtime.InteropServices;
using System.Text.Json;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// <see cref="PlatformManifest.MachineRuntimeIdentifier"/>: the runtime identifier of the machine the code runs on, in the form a manifest's <c>rid</c> takes,
/// so that a platform built for the machine is the one whose manifest names it.
/// </summary>
[TestClass]
public sealed class MachineRuntimeIdentifierTests
{
    [TestMethod]
    public void TheIdentifier_IsTheOperatingSystem_AHyphen_AndTheArchitectureOfTheProcess()
    {
        var system = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : OperatingSystem.IsLinux() ? "linux"
            : OperatingSystem.IsFreeBSD() ? "freebsd"
            : null;
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        var expected = system is null ? RuntimeInformation.RuntimeIdentifier : $"{system}-{architecture}";
        Assert.AreEqual(expected, PlatformManifest.MachineRuntimeIdentifier);
    }

    // a manifest names the portable identifier (win-x64, linux-arm64), never a distribution's; the C library is not part of it.
    [TestMethod]
    public void TheIdentifier_IsPortable_InLowerCase()
    {
        StringAssert.Matches(PlatformManifest.MachineRuntimeIdentifier, new System.Text.RegularExpressions.Regex("^(win|osx|linux|freebsd)-[a-z0-9]+$"));
    }

    [TestMethod]
    public void TheIdentifier_IsNotWrittenToAManifest()
    {
        var json = JsonSerializer.Serialize(new PlatformManifest { RuntimeIdentifier = "win-x64" });

        Assert.DoesNotContain(nameof(PlatformManifest.MachineRuntimeIdentifier), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"rid\":\"win-x64\"", json);
    }
}
