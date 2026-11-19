using RDCore.SDK.Model.Values.Intrinsic;
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

    // what the analyzers read of the project's constants and of the installation is the host's to state.
    [TestMethod]
    public void TheProjectsConstants_AndWhetherWin64IsSetOnWindowsOnly_AreReadBackAsTheyWereWritten()
    {
        var read = RoundTrip(new ModuleSemanticModel(Module, [], [])
        {
            ProjectConditionalConstants = [new ConditionalConstantFact("Win64", new VBLongValue(-1)), new ConditionalConstantFact("MODE", new VBStringValue("debug"))],
            Win64OnlySetOnWindows = true,
        });

        Assert.IsNotNull(read.ProjectConditionalConstants);
        var constants = read.ProjectConditionalConstants.Value;
        CollectionAssert.AreEqual(new[] { "Win64", "MODE" }, constants.Select(constant => constant.Name).ToArray());
        Assert.IsTrue(constants[0].Value is VBLongValue { Value: -1 }, constants[0].Value.ToString());
        Assert.IsTrue(constants[1].Value is VBStringValue { Value: "debug" }, constants[1].Value.ToString());
        Assert.IsTrue(read.Win64OnlySetOnWindows);
    }

    [TestMethod]
    public void AProjectThatDefinesNoConstant_HasNone_AndWin64IsSetOnAMacToo()
    {
        var read = RoundTrip(new ModuleSemanticModel(Module, [], []));

        Assert.IsNull(read.ProjectConditionalConstants);
        Assert.IsFalse(read.Win64OnlySetOnWindows);
    }
}
