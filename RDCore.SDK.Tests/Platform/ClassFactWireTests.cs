using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// The classes whose objects a module creates, and the class each variable declared <c>As New</c> creates, travel with the module's model, from the
/// environment host to an analyzer, as the platform writes the semantics.
/// </summary>
[TestClass]
public sealed class ClassFactWireTests
{
    private static readonly Uri Module = new UriBuilder("file://rdcore-test") { Fragment = "Module1" }.Uri;
    private static readonly Uri Variable = new UriBuilder("file://rdcore-test") { Fragment = "Module1.e" }.Uri;
    private static readonly Uri ErrObject = new UriBuilder("rdcore-library://VBA") { Fragment = "ErrObject" }.Uri;

    private static ModuleSemanticsDto RoundTrip(ModuleSemanticModel model)
        => PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single();

    [TestMethod]
    public void TheClassesAModuleCreates_AreReadBackAsTheyWereWritten()
    {
        var written = new ClassFact(ErrObject, "ErrObject", IsCreatable: true, IsRegistered: false);
        var declaration = new DeclarationFact(new SemanticId(Variable), "e", DeclarationKind.Variable, AccessModifier.Private, false,
            new SourceLocation(Module, SourceRange.Empty), null) { AutoInstantiates = new SemanticId(ErrObject) };
        var model = new ModuleSemanticModel(Module, [], []) { Classes = [written], Declarations = [declaration] };

        var read = RoundTrip(model);

        Assert.IsNotNull(read.Classes);
        Assert.AreEqual(written, read.Classes.Value.Single());
        // Uri equality ignores the fragment, which is what names the class.
        Assert.AreEqual(ErrObject.AbsoluteUri, read.Classes.Value.Single().Class.AbsoluteUri);
        Assert.AreEqual(ErrObject.AbsoluteUri, read.Declarations.Single().AutoInstantiates?.AbsoluteUri);
    }

    [TestMethod]
    public void AModuleThatCreatesNoObject_HasNoClasses()
        => Assert.IsNull(RoundTrip(new ModuleSemanticModel(Module, [], [])).Classes);
}
