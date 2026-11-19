using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// Coverage for <see cref="SemanticId"/>: a fragment-aware identity for <see cref="Symbol"/>, safe to
/// use as a dictionary key or set member where <see cref="Uri"/>'s own <c>Equals</c>/<c>GetHashCode</c>
/// (which ignore <see cref="Uri.Fragment"/>) would silently collide two distinct symbols.
/// </summary>
[TestClass]
public sealed class SemanticIdTests
{
    private static Uri Uri(string fragment) => new UriBuilder("file://rdcore-test") { Fragment = fragment }.Uri;

    [TestMethod]
    public void SameUri_AreEqual()
    {
        var first = new SemanticId(Uri("Foo"));
        var second = new SemanticId(Uri("Foo"));

        Assert.IsTrue(first == second);
        Assert.IsTrue(first.Equals(second));
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void UrisDifferingOnlyByFragment_AreNotEqual()
    {
        var foo = new SemanticId(Uri("Foo"));
        var bar = new SemanticId(Uri("Bar"));

        // the very case plain Uri.Equals gets wrong: these two Uris share everything but the
        // fragment, which Uri's own equality ignores but is where a symbol's identity actually lives.
        Assert.IsFalse(foo == bar);
        Assert.IsFalse(foo.Equals(bar));
        Assert.AreNotEqual(foo.GetHashCode(), bar.GetHashCode());
    }

    [TestMethod]
    public void ToString_ReturnsTheAbsoluteUri()
        => Assert.AreEqual(Uri("Foo").AbsoluteUri, new SemanticId(Uri("Foo")).ToString());

    [TestMethod]
    public void Symbol_SemanticId_WrapsItsOwnUri()
    {
        var uri = Uri("Foo");
        var symbol = new VBUserDefinedTypeMemberSymbol(uri, uri, "Foo", ScopeKind.Module, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public);

        // Symbol.Uri is derived (parentUri.Fragment + "." + name), not necessarily the ctor's raw
        // uri argument — SemanticId only needs to track whatever Uri the symbol actually settled on.
        Assert.AreEqual(new SemanticId(symbol.Uri), symbol.SemanticId);
    }

    [TestMethod]
    public void Symbol_GivenAnotherUri_IsIdentifiedByIt()
    {
        var uri = Uri("Foo");
        var symbol = new VBUserDefinedTypeMemberSymbol(uri, uri, "Foo", ScopeKind.Module, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public);
        var moved = symbol with { Uri = Uri("Bar") };

        // the identity is kept with the symbol; a copy given another address is identified by that one, and the original keeps its own.
        Assert.AreEqual(new SemanticId(Uri("Bar")), moved.SemanticId);
        Assert.AreEqual(new SemanticId(Uri("Bar")).GetHashCode(), moved.SemanticId.GetHashCode());
        Assert.AreEqual(new SemanticId(symbol.Uri), symbol.SemanticId);
    }

    [TestMethod]
    public void GivenAnotherUri_HashesThatOne()
    {
        var moved = new SemanticId(Uri("Foo")) with { Uri = Uri("Bar") };

        Assert.AreEqual(new SemanticId(Uri("Bar")), moved);
        Assert.AreEqual(new SemanticId(Uri("Bar")).GetHashCode(), moved.GetHashCode());
        Assert.AreNotEqual(new SemanticId(Uri("Foo")), moved);
    }

    [TestMethod]
    public void TheSameUriInstance_IsEqual()
    {
        var uri = Uri("Foo");

        Assert.AreEqual(new SemanticId(uri), new SemanticId(uri));
    }

    [TestMethod]
    public void Deconstruct_GivesTheUri()
    {
        var uri = Uri("Foo");
        new SemanticId(uri).Deconstruct(out var deconstructed);

        Assert.AreSame(uri, deconstructed);
    }

    [TestMethod]
    public void TheDefaultIdentity_IsEqualToItself_AndToNoSymbolsIdentity()
    {
        Assert.AreEqual(default, default(SemanticId));
        Assert.AreEqual(0, default(SemanticId).GetHashCode());
        Assert.AreNotEqual(new SemanticId(Uri("Foo")), default);
    }
}
