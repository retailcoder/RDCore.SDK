using System.IO.Abstractions.TestingHelpers;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Runtime.Libraries;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// The libraries a project references without its project file listing them: its language's, and RDForms when it has the code of an RDForms form
/// (<see cref="SupportedLanguage.ImplicitReferencesOf"/>), before the ones the file lists.
/// </summary>
[TestClass]
public sealed class ImplicitReferenceTests
{
    private static readonly string Symbols = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "symbols");

    private static DirectoryLibrarySource Source()
    {
        static MockFileData Library(string name) => new($$"""{ "formatVersion": 2, "name": "{{name}}" }""");
        var files = new MockFileSystem(new[] { "VBRUN", "VB", "RDForms", "Scripting" }
            .ToDictionary(name => Path.Combine(Symbols, name + ".json"), Library));
        return new DirectoryLibrarySource(files, Symbols);
    }

    private static RDCoreProject Project(string[] modules, params string[] references) => new()
    {
        Modules = [.. modules.Select(path => new RDCoreModule { RelativeUri = path })],
        References = [.. references.Select(name => new RDCoreReference { Name = name })],
    };

    private static readonly string[] WithAForm = ["Module1.bas", "Forms/Form2.RDFORM"];
    private static readonly string[] WithNoForm = ["Module1.bas", "Form1.frm", "Forms/Form2.xaml"];

    [TestMethod]
    public void AProjectWithAnRDFormsForm_ReferencesRDForms_AfterItsLanguagesOwn()
    {
        CollectionAssert.AreEqual(new[] { "VBRUN", "VB", "RDForms" }, SupportedLanguages.VB6.ImplicitReferencesOf(WithAForm).ToArray());
        CollectionAssert.AreEqual(new[] { "RDForms" }, SupportedLanguages.RDVBA.ImplicitReferencesOf(WithAForm).ToArray());
    }

    [TestMethod]
    public void AProjectWithNoRDFormsForm_ReferencesItsLanguagesOwn()
    {
        CollectionAssert.AreEqual(new[] { "VBRUN", "VB" }, SupportedLanguages.VB6.ImplicitReferencesOf(WithNoForm).ToArray());
        Assert.IsEmpty(SupportedLanguages.RDVBA.ImplicitReferencesOf([null, .. WithNoForm]));
    }

    // a script runs under Windows Script Host, whose WScript object and WScript.Shell a script uses without naming a library.
    [TestMethod]
    public void AScript_ReferencesTheLibrariesOfWindowsScriptHost()
        => CollectionAssert.AreEqual(new[] { "IHost", "IWshRuntimeLibrary" }, SupportedLanguages.VBScript.ImplicitReferencesOf(["Backup.vbs"]).ToArray());

    [TestMethod]
    public void ALanguageThatHasRDForms_ReferencesItOnce()
    {
        var language = new SupportedLanguage("forms", "Forms", "*.bas", "*.rdform") { ImplicitReferences = ["rdforms"] };

        CollectionAssert.AreEqual(new[] { "rdforms" }, language.ImplicitReferencesOf(WithAForm).ToArray());
    }

    [TestMethod]
    public void AProjectsLibraries_AreItsImplicitReferences_ThenTheOnesItsFileLists()
    {
        var libraries = LibrarySymbolProvider.Load(Source(), Project(WithAForm, "Scripting"), SupportedLanguages.VB6);

        Assert.IsEmpty(libraries.Problems);
        CollectionAssert.AreEqual(new[] { ("VBRUN", 1), ("VB", 2), ("RDForms", 3), ("Scripting", 4) },
            libraries.Priorities.OrderBy(priority => priority.Value).Select(priority => (priority.Key, priority.Value)).ToArray());
    }

    // a reference to another project of the workspace keeps its place after the implicit references, RDForms among them, and is not loaded: the source
    // has a library of its name, which is not the project's.
    [TestMethod]
    public void AReferenceToAProject_KeepsItsPlaceAfterRDForms_AndIsNotLoaded()
    {
        var project = Project(WithAForm) with
        {
            References = [new RDCoreReference { Name = "Scripting", IsProject = true }, new RDCoreReference { Name = "VB" }],
        };

        var libraries = LibrarySymbolProvider.Load(Source(), project, SupportedLanguages.RDVBA);

        Assert.IsEmpty(libraries.Problems);
        CollectionAssert.AreEquivalent(new[] { "RDForms", "VB" }, libraries.Libraries.Select(library => library.Name).ToArray());
        CollectionAssert.AreEqual(new[] { ("RDForms", 1), ("Scripting", 2), ("VB", 3) },
            libraries.Priorities.OrderBy(priority => priority.Value).Select(priority => (priority.Key, priority.Value)).ToArray());
        CollectionAssert.AreEqual(new[] { new ReferencePriorityInfo("VBA", 0), new("RDForms", 1), new("Scripting", 2), new("VB", 3) },
            LibrarySymbolProvider.ReferencePriorities(project, SupportedLanguages.RDVBA).ToArray());
    }

    [TestMethod]
    public void AReferenceToAProject_IsItsNameAlone_WhateverTypeLibraryItGives()
    {
        var project = Project(WithAForm) with
        {
            References = [new RDCoreReference { Name = "Scripting", IsProject = true, Guid = Guid.NewGuid(), Major = 2, Minor = 1 }, new RDCoreReference { Name = "Scripting" }],
        };

        var problem = LibrarySymbolProvider.Load(Source(), project, SupportedLanguages.RDVBA).Problems.Single();

        Assert.AreEqual(LibraryProblemKind.DuplicateName, problem.Kind);
        Assert.AreEqual(new LibraryIdentity("Scripting").ToString(), problem.Related[0]);
    }

    [TestMethod]
    public void AProjectWithNoRDFormsForm_DoesNotLoadRDForms()
    {
        var libraries = LibrarySymbolProvider.Load(Source(), Project(WithNoForm, "Scripting"));

        CollectionAssert.AreEqual(new[] { "Scripting" }, libraries.Libraries.Select(library => library.Name).ToArray());
    }

    [TestMethod]
    public void TheRanksOfAProjectsReferences_PutTheStandardLibraryFirst_AndLeaveOutASecondReference()
    {
        var ranks = LibrarySymbolProvider.ReferencePriorities(Project(WithAForm, "Scripting", "VBA", "vb", "RDForms"), SupportedLanguages.VB6);

        CollectionAssert.AreEqual(
            new[] { new ReferencePriorityInfo("VBA", 0), new("VBRUN", 1), new("VB", 2), new("RDForms", 3), new("Scripting", 4) }, ranks.ToArray());
    }

    [TestMethod]
    public void TheRanksOfAProjectsReferences_LeaveOutTheStandardLibrary_ByItsTypeLibrary()
    {
        var project = Project(WithNoForm, "Scripting") with
        {
            References = [new RDCoreReference { Name = "VBA6", Guid = RDCoreReference.StandardLibraryGuid }, new RDCoreReference { Name = "Scripting" }],
        };

        CollectionAssert.AreEqual(new[] { new ReferencePriorityInfo("VBA", 0), new("Scripting", 1) }, LibrarySymbolProvider.ReferencePriorities(project).ToArray());
    }

    [TestMethod]
    public void TheRanksOfAProjectsReferences_AreTheOrderItsLibrariesLoadIn()
    {
        var project = Project(WithAForm, "Scripting");
        var loaded = LibrarySymbolProvider.Load(Source(), project).Priorities;

        foreach (var rank in LibrarySymbolProvider.ReferencePriorities(project).Skip(1))
            Assert.AreEqual(rank.Priority, loaded[rank.Name], rank.Name);
    }
}
