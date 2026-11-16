using System.IO.Abstractions.TestingHelpers;
using RDCore.SDK.Runtime.Libraries;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// Several sources of library descriptions in order (<see cref="CompositeLibrarySource"/>): the first that has a library of a name gives it, and the others
/// give every other library.
/// </summary>
[TestClass]
public sealed class CompositeLibrarySourceTests
{
    private static string Library(string name, string description, string? guid = null) => $$"""
        { "formatVersion": 2, "name": "{{name}}", "description": "{{description}}"{{(guid is null ? "" : $$""", "origin": { "guid": "{{guid}}", "version": "6.0" }""")}} }
        """;

    // a package's folder that describes VB, ahead of the platform's Symbols folder, which describes VB and DAO too.
    private static CompositeLibrarySource Sources()
    {
        var files = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"c:\package\VB.json"] = new(Library("VB", "the package's", "{FCFB3D2E-A0FA-1068-A738-08002B3371B5}")),
            [@"c:\symbols\VB.json"] = new(Library("VB", "the folder's", "{FCFB3D2E-A0FA-1068-A738-08002B3371B5}")),
            [@"c:\symbols\DAO.json"] = new(Library("DAO", "the folder's", "{00025E04-0000-0000-C000-000000000046}")),
        });
        return new CompositeLibrarySource(new DirectoryLibrarySource(files, @"c:\package"), new DirectoryLibrarySource(files, @"c:\symbols"));
    }

    [TestMethod]
    public void ALibraryOfTheFirstSource_IsTheFirstSources_AndAnotherIsTheNextOnes()
    {
        var sources = Sources();

        Assert.IsTrue(sources.TryGet("vb", out var vb));
        Assert.AreEqual("the package's", vb.Description);
        Assert.IsTrue(sources.TryGet("DAO", out var dao));
        Assert.AreEqual("the folder's", dao.Description);
        Assert.IsFalse(sources.TryGet("Excel", out _));
    }

    [TestMethod]
    public void TheDescriptionsOfAName_AreTheFirstSourcesThatHasAny()
    {
        var sources = Sources();

        CollectionAssert.AreEqual(new[] { "the package's" }, sources.DescriptionsOf("VB").Select(description => description.Description).ToArray());
        CollectionAssert.AreEqual(new[] { "the folder's" }, sources.DescriptionsOf("DAO").Select(description => description.Description).ToArray());
        Assert.IsEmpty(sources.DescriptionsOf("Excel"));
    }

    [TestMethod]
    public void TheNames_AreEverySourcesOnce()
        => CollectionAssert.AreEquivalent(new[] { "VB", "DAO" }, Sources().Names.ToArray());

    [TestMethod]
    public void AReferenceByItsGuid_FindsTheFirstSourcesLibrary()
    {
        var sources = Sources();

        Assert.IsTrue(((ILibrarySource)sources).TryGetByGuid(Guid.Parse("FCFB3D2E-A0FA-1068-A738-08002B3371B5"), out var vb));
        Assert.AreEqual("the package's", vb.Description);
        Assert.IsTrue(((ILibrarySource)sources).TryGet(new LibraryIdentity("VB", Guid.Parse("FCFB3D2E-A0FA-1068-A738-08002B3371B5")), out var identified));
        Assert.AreEqual("the package's", identified.Description);
    }

    [TestMethod]
    public void NoSource_HasNoLibrary()
    {
        var none = new CompositeLibrarySource();

        Assert.IsFalse(none.TryGet("VB", out _));
        Assert.IsEmpty(none.Names);
    }
}
