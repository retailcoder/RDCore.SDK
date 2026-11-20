using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// What a handler or an implementation is bound to travels with the module's model, from the environment host to an analyzer, as the platform writes the
/// semantics.
/// </summary>
[TestClass]
public sealed class DeclarationBindingWireTests
{
    private static readonly Uri Module = new UriBuilder("file://rdcore-test") { Fragment = "Watcher" }.Uri;
    private static readonly Uri Handler = new UriBuilder("file://rdcore-test") { Fragment = "Watcher.source_Spun" }.Uri;
    private static readonly Uri Source = new UriBuilder("file://rdcore-test") { Fragment = "Watcher.source" }.Uri;
    private static readonly Uri Spun = new UriBuilder("file://rdcore-test") { Fragment = "Spinner.Spun" }.Uri;

    private static DeclarationFactDto RoundTrip(DeclarationRole role, DeclarationBinding? binding, bool isUsedByReferencingProjects = false)
    {
        var declaration = new DeclarationFact(new SemanticId(Handler), "source_Spun", DeclarationKind.Procedure, AccessModifier.Private, false,
            new SourceLocation(Module, SourceRange.Empty), null) { Role = role, Binding = binding, IsUsedByReferencingProjects = isUsedByReferencingProjects };
        var model = new ModuleSemanticModel(Module, [], []) { Declarations = [declaration] };
        return PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single()
            .Declarations.Single();
    }

    [TestMethod]
    public void AHandlersBinding_IsReadBackAsItWasWritten()
    {
        var read = RoundTrip(DeclarationRole.EventHandler,
            new DeclarationBinding(new SemanticId(Handler), DeclarationRole.EventHandler, new SemanticId(Source), new SemanticId(Spun)));

        Assert.IsNotNull(read.Binding);
        // Uri equality ignores the fragment, which is what names the symbol.
        Assert.AreEqual(Source.AbsoluteUri, read.Binding.Source.AbsoluteUri);
        Assert.AreEqual(Spun.AbsoluteUri, read.Binding.Bound?.AbsoluteUri);
    }

    [TestMethod]
    public void AnUnmatchedHandler_IsBoundToItsSource_AndToNoEvent()
    {
        var read = RoundTrip(DeclarationRole.UnmatchedEventHandler,
            new DeclarationBinding(new SemanticId(Handler), DeclarationRole.UnmatchedEventHandler, new SemanticId(Source), null));

        Assert.AreEqual(Source.AbsoluteUri, read.Binding?.Source.AbsoluteUri);
        Assert.IsNull(read.Binding?.Bound);
    }

    [TestMethod]
    public void ADeclarationBoundToNothing_HasNoBinding()
        => Assert.IsNull(RoundTrip(DeclarationRole.None, null).Binding);

    // a member of a library that its referencing projects can use counts as used, and its references stay unknown.
    [TestMethod]
    public void ADeclarationUsedByReferencingProjects_IsReadBackAsSo()
    {
        var read = RoundTrip(DeclarationRole.None, null, isUsedByReferencingProjects: true);

        Assert.IsTrue(read.IsUsedByReferencingProjects);
        Assert.IsNull(read.References);
    }

    [TestMethod]
    public void ADeclarationOfAnyOtherProject_IsNotUsedByReferencingProjects()
        => Assert.IsFalse(RoundTrip(DeclarationRole.None, null).IsUsedByReferencingProjects);
}
