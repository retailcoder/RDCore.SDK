using System.Reflection;
using RDCore.SDK;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// The version of the RDCore.SDK a program runs with, as the SDK's build stamps it.
/// </summary>
[TestClass]
public sealed class RDCoreSdkTests
{
    [TestMethod]
    public void TheSdksVersion_IsTheOneItsBuildStamps()
    {
        var built = typeof(RDCoreSdkTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "BuiltVersion").Value;

        Assert.AreEqual(PlatformVersion.Parse(built!), RDCoreSdk.Version);
    }

    [TestMethod]
    public void TheStampedVersion_IsReadWithoutItsBuildMetadata()
        => Assert.AreEqual("0.1.2-local.abc1234", RDCoreSdk.Of("0.1.2-local.abc1234+2062f9857560619aec056b37ac0181c84c0c6e6a", new Version(0, 1, 2, 0)).Text);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not a version")]
    public void AnAssemblyWithNoStampedVersion_ReportsItsNumericOne(string? informationalVersion)
        => Assert.AreEqual("0.1.2", RDCoreSdk.Of(informationalVersion, new Version(0, 1, 2, 0)).Text);

    [TestMethod]
    public void AnAssemblyWithNoVersionAtAll_Reports000()
        => Assert.AreEqual("0.0.0", RDCoreSdk.Of(null, null).Text);
}
