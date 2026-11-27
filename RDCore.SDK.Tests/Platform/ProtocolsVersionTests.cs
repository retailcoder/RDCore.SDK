using System.Reflection;
using RDCore.SDK;
using RDCore.SDK.Platform;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Platform;

/// <summary>
/// The SDK depends on an RDCore.SDK.Protocols of its own major and minor version, so a client that references Protocols alone can compare
/// Protocols' version (<see cref="RDCoreSdk.Version"/>) with the SDK a platform was built with.
/// </summary>
[TestClass]
public sealed class ProtocolsVersionTests
{
    [TestMethod]
    public void TheProtocolsTheSdkRunsWith_IsCompatibleWithTheSdk()
    {
        var stamped = typeof(RDCoreDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.IsTrue(PlatformVersion.TryParse(stamped, out var sdk), $"the SDK's stamped version: {stamped}");
        Assert.IsTrue(RDCoreSdk.Version.IsCompatibleWith(sdk), $"RDCore.SDK.Protocols {RDCoreSdk.Version.Text}, RDCore.SDK {sdk.Text}");
    }
}
