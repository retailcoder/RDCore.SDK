using Newtonsoft.Json.Linq;
using RDCore.SDK.Platform.Components;

namespace RDCore.Tests.Platform;

/// <summary>
/// <c>rdcore/host/project/reload</c> carries the language the language server decided for the project, or none, which keeps the project's current one.
/// </summary>
[TestClass]
public sealed class HostProjectReloadParamsTests
{
    private static readonly Uri Root = new("file:///C:/Dev/Probe/");

    [TestMethod]
    public void TheLanguage_IsReadBackFromTheWire()
    {
        var onTheWire = JToken.FromObject(new HostProjectReloadParams { WorkspaceRoot = Root, Language = "vb6" });

        var read = onTheWire.ToObject<HostProjectReloadParams>()!;

        Assert.AreEqual("vb6", read.Language);
        Assert.AreEqual(Root, read.WorkspaceRoot);
    }

    [TestMethod]
    public void ARequestOfNoLanguage_KeepsTheCurrentOne()
        => Assert.IsNull(JObject.Parse("{ \"workspaceRoot\": \"file:///C:/Dev/Probe/\" }").ToObject<HostProjectReloadParams>()!.Language);
}
