using System.IO.Abstractions.TestingHelpers;
using RDCore.SDK.Runtime.Libraries;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// A reference to another project of the workspace is not a library (<see cref="RDCoreReference.IsProject"/>): it is not loaded, and it keeps its place in
/// the order of the references, so its number is the precedence of the project's members and the libraries after it keep theirs
/// (<strong>MS-VBAL §4.1</strong>).
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.3.1.2 References")]
public sealed class ProjectReferencePrecedenceTests
{
    private static readonly string Root = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "symbols");

    private static DirectoryLibrarySource Source(params string[] names)
        => new(new MockFileSystem(names.ToDictionary(
            name => Path.Combine(Root, $"{name}.json"),
            name => new MockFileData($$"""{ "formatVersion": 2, "name": "{{name}}", "description": "the {{name}} library" }"""))), Root);

    private static RDCoreReference Library(string name) => new() { Name = name };

    private static RDCoreReference Project(string name) => new() { Name = name, IsProject = true };

    [TestMethod]
    public void AReferenceToAProject_IsNotLoaded_AndKeepsItsPlaceInTheOrder()
    {
        var libraries = LibrarySymbolProvider.Load(Source("VB", "MSForms"), [Library("VB"), Project("NavalLib"), Library("MSForms")]);

        CollectionAssert.AreEqual(new[] { "VB", "MSForms" }, libraries.Libraries.Select(library => library.Name).ToArray());
        Assert.IsEmpty(libraries.Problems, "a project is not a library the source is missing");
        Assert.AreEqual(1, libraries.Priorities["VB"]);
        Assert.AreEqual(2, libraries.Priorities["NavalLib"]);
        Assert.AreEqual(3, libraries.Priorities["MSForms"]);
    }

    // a language's implicit references come before the ones the project lists: a project keeps its place after them, and the library after it too.
    [TestMethod]
    public void AReferenceToAProject_KeepsItsPlace_AfterTheLanguagesImplicitReferences()
    {
        var libraries = LibrarySymbolProvider.Load(Source("VBRUN", "VB", "MSForms"), [Project("NavalLib"), Library("MSForms")], SupportedLanguages.VB6);

        CollectionAssert.AreEqual(new[] { "VBRUN", "VB", "MSForms" }, libraries.Libraries.Select(library => library.Name).ToArray());
        Assert.IsEmpty(libraries.Problems);
        Assert.AreEqual(3, libraries.Priorities["NavalLib"]);
        Assert.AreEqual(4, libraries.Priorities["MSForms"]);
    }

    // the source has a library of the project's name, and it is not the reference: a project is never asked of the source.
    [TestMethod]
    public void AReferenceToAProject_IsTheProject_WhenTheSourceHasALibraryOfItsName()
    {
        var libraries = LibrarySymbolProvider.Load(Source("NavalLib"), [Project("NavalLib")]);

        Assert.IsEmpty(libraries.Libraries);
        Assert.IsEmpty(libraries.Problems);
        Assert.AreEqual(1, libraries.Priorities["NavalLib"]);
    }

    // a name is one reference of a project: a library of a project's name, referenced after it, is the name referenced twice, and is not loaded.
    [TestMethod]
    public void AProjectAndALibraryOfOneName_AreOneNameReferencedTwice()
    {
        var libraries = LibrarySymbolProvider.Load(Source("NavalLib"), [Project("NavalLib"), Library("navallib")]);

        Assert.IsEmpty(libraries.Libraries);
        var problem = libraries.Problems.Single();
        Assert.AreEqual(LibraryProblemKind.DuplicateName, problem.Kind);
        Assert.AreEqual(1, libraries.Priorities["NavalLib"]);
    }

    // the same, the library first: the library is loaded, and the project is the name referenced twice.
    [TestMethod]
    public void ALibraryAndAProjectOfOneName_AreOneNameReferencedTwice_AndTheLibraryIsLoaded()
    {
        var libraries = LibrarySymbolProvider.Load(Source("NavalLib"), [Library("NavalLib"), Project("NavalLib")]);

        CollectionAssert.AreEqual(new[] { "NavalLib" }, libraries.Libraries.Select(library => library.Name).ToArray());
        Assert.AreEqual(LibraryProblemKind.DuplicateName, libraries.Problems.Single().Kind);
        Assert.AreEqual(1, libraries.Priorities["NavalLib"]);
    }
}
