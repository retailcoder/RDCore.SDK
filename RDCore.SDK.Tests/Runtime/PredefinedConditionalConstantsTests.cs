using RDCore.SDK.Runtime;
using RDCore.SDK.Runtime.Abstract.Execution;
using RDCore.SDK.Server.Configuration;

namespace RDCore.Tests.Runtime;

/// <summary>
/// The predefined conditional-compilation constants of an environment: one test of each operating system, <c>Is64BitEnv</c> for its bitness, and
/// <c>Win64</c> as MS-VBA defines it, on a 64-bit Mac too unless the environment sets it on Windows only.
/// </summary>
[TestClass]
public sealed class PredefinedConditionalConstantsTests
{
    private static string TrueIn(VBOperatingSystem system, bool is64Bit, bool win64OnlySetOnWindows = false)
        => string.Join(" ", PredefinedConditionalConstants.Of(new RuntimeEnvironmentProfile(is64Bit, 0, 1252, false, OperatingSystem: system,
                Win64OnlySetOnWindows: win64OnlySetOnWindows, Configuration: VBBuildConfiguration.Release))
            .Where(constant => constant.Value == PredefinedConditionalConstants.True).Select(constant => constant.Name));

    [TestMethod]
    [DataRow(VBOperatingSystem.Windows, false, "Win32 VBA6 VBA7", DisplayName = "32-bit Windows")]
    [DataRow(VBOperatingSystem.Windows, true, "Win32 Win64 Is64BitEnv VBA6 VBA7", DisplayName = "64-bit Windows")]
    [DataRow(VBOperatingSystem.MacOS, true, "Win64 Mac Is64BitEnv VBA6 VBA7", DisplayName = "64-bit Mac")]
    [DataRow(VBOperatingSystem.MacOS, false, "Mac VBA6 VBA7", DisplayName = "32-bit Mac")]
    [DataRow(VBOperatingSystem.Linux, true, "Linux Is64BitEnv VBA6 VBA7", DisplayName = "64-bit Linux")]
    [DataRow(VBOperatingSystem.Linux, false, "Linux VBA6 VBA7", DisplayName = "32-bit Linux")]
    [DataRow(VBOperatingSystem.Other, true, "Is64BitEnv VBA6 VBA7", DisplayName = "another 64-bit system")]
    public void EachEnvironment_HasTheConstantsOfItsSystemAndItsBitness(VBOperatingSystem system, bool is64Bit, string expected)
        => Assert.AreEqual(expected, TrueIn(system, is64Bit));

    [TestMethod]
    [DataRow(VBOperatingSystem.Windows, "Win32 Win64 Is64BitEnv VBA6 VBA7", DisplayName = "64-bit Windows")]
    [DataRow(VBOperatingSystem.MacOS, "Mac Is64BitEnv VBA6 VBA7", DisplayName = "64-bit Mac")]
    [DataRow(VBOperatingSystem.Linux, "Linux Is64BitEnv VBA6 VBA7", DisplayName = "64-bit Linux")]
    public void Win64OnlySetOnWindows_LeavesWin64OnWindowsAlone(VBOperatingSystem system, string expected)
        => Assert.AreEqual(expected, TrueIn(system, is64Bit: true, win64OnlySetOnWindows: true));

    [TestMethod]
    public void EveryEnvironment_DefinesTheSameNames()
        => CollectionAssert.AreEqual(
            new[] { "DEBUG", "Win16", "Win32", "Win64", "Mac", "Linux", "Is64BitEnv", "VBA6", "VBA7" },
            PredefinedConditionalConstants.Of(RuntimeEnvironmentProfile.Default).Select(constant => constant.Name).ToArray());

    [TestMethod]
    public void Linux64_IsRetired_AndNamesWhatTestsTheSame()
    {
        Assert.IsFalse(PredefinedConditionalConstants.Names.Contains("Linux64"));
        Assert.IsFalse(PredefinedConditionalConstants.CanBeTrue("linux64"));
        Assert.AreEqual("Linux And Is64BitEnv", PredefinedConditionalConstants.Retired["LINUX64"]);
    }

    [TestMethod]
    public void TheSetting_ReachesTheProfile_FromTheOptions()
    {
        Assert.IsTrue(RuntimeEnvironmentProfile.From(new SdkEnvironmentOptions { Win64OnlySetOnWindows = true }).Win64OnlySetOnWindows);
        Assert.IsFalse(RuntimeEnvironmentProfile.From(new SdkEnvironmentOptions()).Win64OnlySetOnWindows);
    }
}
