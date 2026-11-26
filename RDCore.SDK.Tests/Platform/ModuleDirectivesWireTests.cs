using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// The directives a module is under travel with the module's model, from the environment host to an analyzer: its options as every part of it states
/// them.
/// </summary>
[TestClass]
public sealed class ModuleDirectivesWireTests
{
    private static readonly Uri Module = new UriBuilder("file://rdcore-test") { Fragment = "Module1" }.Uri;

    private static ModuleSemanticsDto RoundTrip(ModuleSemanticModel model)
        => PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single();

    [TestMethod]
    public void TheDirectivesOfAModule_AreReadBackAsTheyWereWritten()
    {
        var directives = new ModuleDirectives(Explicit: true, Compare: OptionCompare.Text, Base: 1, PrivateModule: true);

        var read = RoundTrip(new ModuleSemanticModel(Module, [], []) { Directives = directives });

        Assert.AreEqual(directives, read.Directives);
    }

    [TestMethod]
    public void AHostThatDoesNotSay_SendsNoDirectives()
        => Assert.IsNull(RoundTrip(new ModuleSemanticModel(Module, [], [])).Directives);
}
