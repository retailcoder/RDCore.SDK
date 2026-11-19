using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// The names of the document modules a module's names can refer to travel with the module's model, from the environment host to an analyzer, as the
/// platform writes the semantics.
/// </summary>
[TestClass]
public sealed class DocumentModulesWireTests
{
    private static readonly Uri Module = new UriBuilder("file://rdcore-test") { Fragment = "Module1" }.Uri;

    private static ModuleSemanticsDto RoundTrip(ModuleSemanticModel model)
        => PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single();

    [TestMethod]
    public void TheDocumentModulesAModuleCanName_AreReadBackAsTheyWereWritten()
    {
        var read = RoundTrip(new ModuleSemanticModel(Module, [], []) { DocumentModules = ["ThisWorkbook", "Feuil1"] });

        Assert.IsNotNull(read.DocumentModules);
        CollectionAssert.AreEqual(new[] { "ThisWorkbook", "Feuil1" }, read.DocumentModules.Value.ToArray());
    }

    [TestMethod]
    public void AProjectWithNoDocumentModule_HasNone()
        => Assert.IsNull(RoundTrip(new ModuleSemanticModel(Module, [], [])).DocumentModules);
}
