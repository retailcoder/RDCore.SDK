using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// The names a module's directives read that nothing defines travel with the module's model, from the environment host to an analyzer.
/// </summary>
[TestClass]
public sealed class UndefinedConditionalConstantsWireTests
{
    private static readonly Uri Module = new UriBuilder("file://rdcore-test") { Fragment = "Module1" }.Uri;

    private static ModuleSemanticsDto RoundTrip(ModuleSemanticModel model)
        => PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single();

    [TestMethod]
    public void TheUndefinedConstantsOfAModule_AreReadBackAsTheyWereWritten()
    {
        var read = RoundTrip(new ModuleSemanticModel(Module, [], []) { UndefinedConditionalConstants = ["Linux64", "DEV"] });

        Assert.IsNotNull(read.UndefinedConditionalConstants);
        CollectionAssert.AreEqual(new[] { "Linux64", "DEV" }, read.UndefinedConditionalConstants.Value.ToArray());
    }

    [TestMethod]
    public void AModuleWhoseEveryConstantIsDefined_HasNone()
        => Assert.IsNull(RoundTrip(new ModuleSemanticModel(Module, [], [])).UndefinedConditionalConstants);
}
